using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services;

public class HomeScoringService : IHomeScoringService
{
    private readonly IPropertyRepository _propertyRepo;
    private const decimal AnnualInterestRate = 0.065m;
    private const int LoanTermMonths = 360; // 30 years

    public HomeScoringService(IPropertyRepository propertyRepo)
    {
        _propertyRepo = propertyRepo;
    }

    public async Task<List<ScoredPropertyDto>> GetTopPropertiesAsync(
        HomeSearchPreferencesDto prefs,
        int top = 10,
        CancellationToken cancellationToken = default)
    {
        // 1. Load and apply hard filters via repository
        var properties = await _propertyRepo.GetFilteredAsync(
            prefs.MaxPrice,
            prefs.MinBedrooms,
            prefs.MinBathrooms,
            prefs.MinSqft,
            prefs.MaxCommuteMin,
            cancellationToken);

        if (properties.Count == 0)
            return [];

        // 2. Normalize weights
        var totalWeight = prefs.WeightAffordability + prefs.WeightNeighborhood
            + prefs.WeightSize + prefs.WeightAppreciation + prefs.WeightCondition
            + prefs.WeightCommute + prefs.WeightAmenities + prefs.WeightTaxUtilities
            + prefs.WeightResale + prefs.WeightEnvironment;

        if (totalWeight <= 0) totalWeight = 1.0;

        double Norm(double w) => w / totalWeight;

        // 3. Score each property
        var scored = properties.Select(p =>
        {
            var monthlyCost = EstimateMonthlyCost(p);

            var affordability = ScoreAffordability(monthlyCost, prefs.MaxMonthlyBudget);
            var neighborhood = ScoreNeighborhood(p);
            var size = ScoreSize(p, prefs);
            var appreciation = Clamp(p.FutureAppreciation);
            var condition = ScoreCondition(p);
            var commute = ScoreCommute(p.CommuteMin, prefs.MaxCommuteMin);
            var amenities = Clamp(p.AmenitiesScore);
            var taxUtil = ScoreTaxUtilities(p);
            var resale = Clamp(p.ResalePotential);
            var environment = ScoreEnvironment(p);

            var composite =
                Norm(prefs.WeightAffordability) * affordability
              + Norm(prefs.WeightNeighborhood) * neighborhood
              + Norm(prefs.WeightSize) * size
              + Norm(prefs.WeightAppreciation) * appreciation
              + Norm(prefs.WeightCondition) * condition
              + Norm(prefs.WeightCommute) * commute
              + Norm(prefs.WeightAmenities) * amenities
              + Norm(prefs.WeightTaxUtilities) * taxUtil
              + Norm(prefs.WeightResale) * resale
              + Norm(prefs.WeightEnvironment) * environment;

            return new ScoredPropertyDto
            {
                PropertyId = p.Id,
                Street = p.Street,
                City = p.City,
                ZipCode = p.ZipCode,
                Price = p.Price,
                Bedrooms = p.Bedrooms,
                Bathrooms = p.Bathrooms,
                LotSqft = p.LotSqft,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                EstimatedMonthlyCost = monthlyCost,
                AffordabilityScore = affordability,
                NeighborhoodScore = neighborhood,
                SizeScore = size,
                AppreciationScore = appreciation,
                ConditionScore = condition,
                CommuteScore = commute,
                AmenitiesScore = amenities,
                TaxUtilitiesScore = taxUtil,
                ResaleScore = resale,
                EnvironmentScore = environment,
                CompositeScore = Math.Round(composite, 2)
            };
        })
        .OrderByDescending(s => s.CompositeScore)
        .Take(top)
        .ToList();

        // Assign rank
        for (int i = 0; i < scored.Count; i++)
            scored[i].Rank = i + 1;

        return scored;
    }

    // --- Sub-score methods ---

    private static decimal EstimateMonthlyCost(Property p)
    {
        var monthlyRate = AnnualInterestRate / 12m;
        var loanAmount = p.Price * 0.80m; // 20% down
        decimal mortgage;
        if (monthlyRate > 0)
        {
            var factor = (double)monthlyRate * Math.Pow(1 + (double)monthlyRate, LoanTermMonths)
                       / (Math.Pow(1 + (double)monthlyRate, LoanTermMonths) - 1);
            mortgage = loanAmount * (decimal)factor;
        }
        else
        {
            mortgage = loanAmount / LoanTermMonths;
        }

        return Math.Round(mortgage + (p.PropertyTax / 12m) + p.HoaFee + p.Utilities, 2);
    }

    private static double ScoreAffordability(decimal monthlyCost, decimal budget)
    {
        if (budget <= 0) return 50;
        var ratio = (double)(monthlyCost / budget);
        return Clamp((1.0 - ratio) * 100);
    }

    private static double ScoreNeighborhood(Property p)
    {
        return Clamp((p.SchoolRating + p.Walkability + p.TransitAccess + (100 - p.CrimeScore)) / 4.0);
    }

    private static double ScoreSize(Property p, HomeSearchPreferencesDto prefs)
    {
        var sqftRatio = prefs.MinSqft > 0 ? (double)p.LotSqft / prefs.MinSqft : 1.0;
        var bedroomBonus = (p.Bedrooms - prefs.MinBedrooms) * 10.0;
        return Clamp(Math.Min(sqftRatio * 50, 100) + bedroomBonus);
    }

    private static double ScoreCondition(Property p)
    {
        var avg = (p.RoofCondition + p.AcCondition + p.PlumbingCondition + p.ElectricalCondition) / 4.0;
        var reno = p.LastRenovation.HasValue ? Math.Max(0, 10 - (DateTime.UtcNow.Year - p.LastRenovation.Value)) * 2.0 : 0;
        return Clamp(avg + reno);
    }

    private static double ScoreCommute(int commuteMin, int maxCommute)
    {
        if (maxCommute <= 0) return 50;
        return Clamp((1.0 - (double)commuteMin / maxCommute) * 100);
    }

    private static double ScoreTaxUtilities(Property p)
    {
        var annual = (double)(p.PropertyTax + (p.Utilities * 12) + (p.HoaFee * 12));
        return Clamp(Math.Max(0, 100 - (annual / 300.0)));
    }

    private static double ScoreEnvironment(Property p)
    {
        return Clamp((100 - p.FloodRisk + (100 - p.NoiseLevel)) / 2.0);
    }

    private static double Clamp(double value) => Math.Clamp(value, 0, 100);
}