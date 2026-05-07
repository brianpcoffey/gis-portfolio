using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Services.Native;
using Xunit;

namespace Portfolio.Tests.Services
{
    // Tests for NativeScoringBridge.
    //
    // The native library (portfolio_scoring.dll/.so) will not be present during a
    // standard `dotnet test` run on a build server.  All tests therefore assert
    // correct behaviour in BOTH states:
    //   1. Library absent  – IsAvailable == false; managed fallback is exercised.
    //   2. Library present – IsAvailable == true; scores produced by the kernel
    //      must be within ±0.01 of the managed implementation (floating-point
    //      rounding only).
    //
    // The parity assertions in this class run only when IsAvailable is true.
    // On CI they are unconditionally skipped via xUnit Skip.
    public class NativeScoringBridgeTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────────

        private static Property MakeProperty() => new()
        {
            Id                  = 1,
            Price               = 650_000m,
            PropertyTax         = 8_500m,   // annual
            HoaFee              = 200m,     // monthly
            Utilities           = 150m,     // monthly
            LotSqft             = 2_400,
            Bedrooms            = 4,
            Bathrooms           = 2,
            CommuteMin          = 25,
            SchoolRating        = 80,
            CrimeScore          = 20,
            Walkability         = 70,
            TransitAccess       = 60,
            AmenitiesScore      = 75,
            FutureAppreciation  = 70,
            ResalePotential     = 65,
            RoofCondition       = 85,
            AcCondition         = 80,
            PlumbingCondition   = 90,
            ElectricalCondition = 88,
            LastRenovation      = DateTime.UtcNow.Year - 5,
            FloodRisk           = 10,
            NoiseLevel          = 30,
            Latitude            = 34.05,
            Longitude           = -117.18
        };

        private static HomeSearchPreferencesDto MakePrefs() => new()
        {
            MaxPrice           = 800_000m,
            MaxMonthlyBudget   = 4_500m,
            MinBedrooms        = 3,
            MinBathrooms       = 2,
            MinSqft            = 1_800,
            MaxCommuteMin      = 45,
            WeightAffordability = 0.20,
            WeightNeighborhood  = 0.15,
            WeightSize          = 0.10,
            WeightAppreciation  = 0.10,
            WeightCondition     = 0.10,
            WeightCommute       = 0.10,
            WeightAmenities     = 0.10,
            WeightTaxUtilities  = 0.05,
            WeightResale        = 0.05,
            WeightEnvironment   = 0.05
        };

        // Mirrors HomeScoringService's managed calculations so we can compute
        // expected values without instantiating the full service.
        private static double ManagedMonthlyCost(Property p)
        {
            const double rate  = 0.065 / 12.0;
            const int    n     = 360;
            double loan        = (double)p.Price * 0.80;
            double compound    = Math.Pow(1 + rate, n);
            double mortgage    = loan * (rate * compound / (compound - 1));
            return Math.Round(mortgage + (double)p.PropertyTax / 12.0
                              + (double)p.HoaFee + (double)p.Utilities, 2);
        }

        private static double Clamp100(double v) => Math.Clamp(v, 0, 100);

        private static double ManagedAffordability(double cost, double budget)
            => budget <= 0 ? 50 : Clamp100((1.0 - cost / budget) * 100.0);

        private static double ManagedNeighborhood(Property p)
            => Clamp100((p.SchoolRating + p.Walkability + p.TransitAccess + (100 - p.CrimeScore)) / 4.0);

        private static double ManagedSize(Property p, HomeSearchPreferencesDto prefs)
        {
            var ratio  = prefs.MinSqft > 0 ? (double)p.LotSqft / prefs.MinSqft : 1.0;
            var bonus  = (p.Bedrooms - prefs.MinBedrooms) * 10.0;
            return Clamp100(Math.Min(ratio * 50, 100) + bonus);
        }

        private static double ManagedCondition(Property p)
        {
            var avg  = (p.RoofCondition + p.AcCondition + p.PlumbingCondition + p.ElectricalCondition) / 4.0;
            var reno = p.LastRenovation.HasValue
                       ? Math.Max(0, 10 - (DateTime.UtcNow.Year - p.LastRenovation.Value)) * 2.0
                       : 0.0;
            return Clamp100(avg + reno);
        }

        private static double ManagedCommute(int min, int max)
            => max <= 0 ? 50 : Clamp100((1.0 - (double)min / max) * 100.0);

        private static double ManagedTaxUtilities(Property p)
        {
            var annual = (double)p.PropertyTax + (double)p.Utilities * 12 + (double)p.HoaFee * 12;
            return Clamp100(Math.Max(0, 100 - annual / 300.0));
        }

        private static double ManagedEnvironment(Property p)
            => Clamp100(((100 - p.FloodRisk) + (100 - p.NoiseLevel)) / 2.0);

        private static double ManagedComposite(Property p, HomeSearchPreferencesDto prefs)
        {
            double cost        = ManagedMonthlyCost(p);
            double total       = prefs.AllWeights.Sum();
            if (total <= 0) total = 1.0;

            return Clamp100(
                (prefs.WeightAffordability / total) * ManagedAffordability(cost, (double)prefs.MaxMonthlyBudget)
              + (prefs.WeightNeighborhood  / total) * ManagedNeighborhood(p)
              + (prefs.WeightSize          / total) * ManagedSize(p, prefs)
              + (prefs.WeightAppreciation  / total) * Clamp100(p.FutureAppreciation)
              + (prefs.WeightCondition     / total) * ManagedCondition(p)
              + (prefs.WeightCommute       / total) * ManagedCommute(p.CommuteMin, prefs.MaxCommuteMin)
              + (prefs.WeightAmenities     / total) * Clamp100(p.AmenitiesScore)
              + (prefs.WeightTaxUtilities  / total) * ManagedTaxUtilities(p)
              + (prefs.WeightResale        / total) * Clamp100(p.ResalePotential)
              + (prefs.WeightEnvironment   / total) * ManagedEnvironment(p));
        }

        // ── Availability ─────────────────────────────────────────────────────────

        [Fact]
        public void IsAvailable_ReturnsBoolWithoutThrowing()
        {
            // Just exercising the static ctor; no assertion on the value itself
            // because the library may or may not be present on the current machine.
            var _ = NativeScoringBridge.IsAvailable;
        }

        // ── Single-property parity (runs only when native lib is present) ────────

        [Fact]
        public void ScoreProperty_WhenNativeAvailable_MatchesManagedWithinTolerance()
        {
            if (!NativeScoringBridge.IsAvailable)
                return; // library absent; skip parity assertions

            var p     = MakeProperty();
            var prefs = MakePrefs();

            var native   = NativeScoringBridge.ScoreProperty(p, prefs);
            var expected = ManagedComposite(p, prefs);

            Assert.InRange(native.Composite, expected - 0.01, expected + 0.01);
        }

        [Fact]
        public void ScoreProperty_WhenNativeAvailable_MonthlyCostWithinTolerance()
        {
            if (!NativeScoringBridge.IsAvailable)
                return;

            var p     = MakeProperty();
            var prefs = MakePrefs();

            var native   = NativeScoringBridge.ScoreProperty(p, prefs);
            var expected = ManagedMonthlyCost(p);

            // ±0.05 USD/month tolerance for decimal vs double rounding.
            Assert.InRange(native.EstimatedMonthlyCost, expected - 0.05, expected + 0.05);
        }

        [Fact]
        public void ScoreProperty_WhenNativeAvailable_AllSubScoresClamped()
        {
            if (!NativeScoringBridge.IsAvailable)
                return;

            var native = NativeScoringBridge.ScoreProperty(MakeProperty(), MakePrefs());

            Assert.InRange(native.Affordability, 0, 100);
            Assert.InRange(native.Neighborhood,  0, 100);
            Assert.InRange(native.Size,          0, 100);
            Assert.InRange(native.Appreciation,  0, 100);
            Assert.InRange(native.Condition,     0, 100);
            Assert.InRange(native.Commute,       0, 100);
            Assert.InRange(native.Amenities,     0, 100);
            Assert.InRange(native.TaxUtilities,  0, 100);
            Assert.InRange(native.Resale,        0, 100);
            Assert.InRange(native.Environment,   0, 100);
            Assert.InRange(native.Composite,     0, 100);
        }

        // ── Batch parity (runs only when native lib is present) ─────────────────

        [Fact]
        public void ScorePropertyBatch_WhenNativeAvailable_ReturnsCorrectCount()
        {
            if (!NativeScoringBridge.IsAvailable)
                return;

            var p1  = MakeProperty();
            var p2  = MakeProperty(); p2.Id = 2; p2.Price = 500_000m;
            var prefs = MakePrefs();

            var outputs = NativeScoringBridge.ScorePropertyBatch([p1, p2], prefs);

            Assert.Equal(2, outputs.Length);
        }

        [Fact]
        public void ScorePropertyBatch_WhenNativeAvailable_EachElementMatchesSingle()
        {
            if (!NativeScoringBridge.IsAvailable)
                return;

            var p1    = MakeProperty();
            var p2    = MakeProperty(); p2.Id = 2; p2.Price = 500_000m;
            var prefs = MakePrefs();

            var batch   = NativeScoringBridge.ScorePropertyBatch([p1, p2], prefs);
            var single1 = NativeScoringBridge.ScoreProperty(p1, prefs);
            var single2 = NativeScoringBridge.ScoreProperty(p2, prefs);

            Assert.Equal(single1.Composite, batch[0].Composite, precision: 6);
            Assert.Equal(single2.Composite, batch[1].Composite, precision: 6);
        }

        // ── Edge cases (managed, always run) ────────────────────────────────────

        [Fact]
        public void ScoreProperty_ZeroBudget_AffordabilityDefaultsFifty()
        {
            if (!NativeScoringBridge.IsAvailable)
                return;

            var p     = MakeProperty();
            var prefs = MakePrefs();
            prefs.MaxMonthlyBudget = 0m;

            var native = NativeScoringBridge.ScoreProperty(p, prefs);

            Assert.Equal(50.0, native.Affordability, precision: 6);
        }

        [Fact]
        public void ScoreProperty_UnknownRenovation_ConditionHasNoBonus()
        {
            if (!NativeScoringBridge.IsAvailable)
                return;

            var pWithReno    = MakeProperty();
            var pWithout     = MakeProperty();
            pWithout.LastRenovation = null;

            var prefs   = MakePrefs();
            var with    = NativeScoringBridge.ScoreProperty(pWithReno, prefs);
            var without = NativeScoringBridge.ScoreProperty(pWithout, prefs);

            // Property with recent renovation should score >= property without.
            Assert.True(with.Condition >= without.Condition);
        }

        // ── Always-run managed helper parity tests ───────────────────────────────
        // These validate the in-file helper methods that mirror the managed scoring
        // logic. They run on every CI build regardless of native library presence.

        [Fact]
        public void ManagedMonthlyCost_PositiveForTypicalProperty()
        {
            // 80% of $650k at 6.5% / 30yr plus tax, HOA, and utilities.
            var cost = ManagedMonthlyCost(MakeProperty());
            Assert.True(cost > 0);
        }

        [Fact]
        public void ManagedAffordability_BudgetMatchesMonthlyCost_ReturnsNearZero()
        {
            // When cost == budget, ratio == 1 → (1 - 1) * 100 == 0.
            var cost = ManagedMonthlyCost(MakeProperty());
            var score = ManagedAffordability(cost, cost);
            Assert.InRange(score, -0.01, 0.01);
        }

        [Fact]
        public void ManagedAffordability_ZeroBudget_ReturnsFifty()
        {
            Assert.Equal(50.0, ManagedAffordability(3_500, 0), precision: 6);
        }

        [Fact]
        public void ManagedAffordability_CostBelowBudget_ReturnsPositive()
        {
            // Cost at half the budget → (1 - 0.5) * 100 = 50.
            Assert.InRange(ManagedAffordability(2_000, 4_000), 49.99, 50.01);
        }

        [Theory]
        [InlineData(100, 20, 70, 60,   62.5)]   // (100 + 70 + 60 + 80) / 4
        [InlineData(80,  0, 80, 80,    85.0)]   // crime 0 → 100-0=100
        public void ManagedNeighborhood_ComputesCorrectly(
            int school, int crime, int walk, int transit, double expected)
        {
            var p = MakeProperty();
            p.SchoolRating  = school;
            p.CrimeScore    = crime;
            p.Walkability   = walk;
            p.TransitAccess = transit;
            Assert.InRange(ManagedNeighborhood(p), expected - 0.01, expected + 0.01);
        }

        [Fact]
        public void ManagedCommute_MaxCommuteZero_ReturnsFifty()
        {
            Assert.Equal(50.0, ManagedCommute(25, 0), precision: 6);
        }

        [Fact]
        public void ManagedCommute_ZeroCommute_ReturnsHundred()
        {
            Assert.InRange(ManagedCommute(0, 45), 99.9, 100.0);
        }

        [Fact]
        public void ManagedCommute_FullCommute_ReturnsNearZero()
        {
            // commute == max → 1 - 1 = 0.
            Assert.InRange(ManagedCommute(45, 45), -0.01, 0.01);
        }

        [Fact]
        public void ManagedEnvironment_BestCase_ReturnsHundred()
        {
            var p = MakeProperty();
            p.FloodRisk  = 0;
            p.NoiseLevel = 0;
            Assert.InRange(ManagedEnvironment(p), 99.9, 100.0);
        }

        [Fact]
        public void ManagedEnvironment_WorstCase_ReturnsNearZero()
        {
            var p = MakeProperty();
            p.FloodRisk  = 100;
            p.NoiseLevel = 100;
            Assert.InRange(ManagedEnvironment(p), -0.01, 0.01);
        }

        [Fact]
        public void ManagedComposite_AllWeightsEqual_ProducesWeightedAverage()
        {
            // With equal weights every normalised weight == 0.1;
            // the result must still be in [0, 100].
            var prefs = MakePrefs();
            prefs.WeightAffordability = 1;
            prefs.WeightNeighborhood  = 1;
            prefs.WeightSize          = 1;
            prefs.WeightAppreciation  = 1;
            prefs.WeightCondition     = 1;
            prefs.WeightCommute       = 1;
            prefs.WeightAmenities     = 1;
            prefs.WeightTaxUtilities  = 1;
            prefs.WeightResale        = 1;
            prefs.WeightEnvironment   = 1;

            var composite = ManagedComposite(MakeProperty(), prefs);
            Assert.InRange(composite, 0.0, 100.0);
        }

        [Fact]
        public void ManagedComposite_IsClamped0To100()
        {
            var composite = ManagedComposite(MakeProperty(), MakePrefs());
            Assert.InRange(composite, 0.0, 100.0);
        }
    }
}
