using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;

namespace Portfolio.Services.Native
{
    // Façade over the native scoring library: probes once at type initialisation, maps the managed
    // Property / HomeSearchPreferencesDto to the native structs, and calls NativeScoringInterop.
    //
    // Deliberately not an interface; HomeScoringService has a complete managed fallback,
    // so no mock is needed in tests.
    internal static class NativeScoringBridge
    {
        private static readonly bool _available = NativeKernelLoader.TryLoad("portfolio_scoring");

        internal static bool IsAvailable => _available;

        // Caller MUST check IsAvailable before calling.
        internal static ScoreOutputNative ScoreProperty(
            Property p,
            HomeSearchPreferencesDto prefs)
        {
            var propNative = MapProperty(p);
            var prefsNative = MapPreferences(prefs);
            NativeScoringInterop.ScoreProperty(in propNative, in prefsNative, out var result);
            return result;
        }

        // Returns a parallel array of ScoreOutputNative with the same element count.
        // Caller MUST check IsAvailable before calling.
        internal static ScoreOutputNative[] ScorePropertyBatch(
            IReadOnlyList<Property> properties,
            HomeSearchPreferencesDto prefs)
        {
            var count = properties.Count;
            var inputs = new PropertyInputNative[count];
            var outputs = new ScoreOutputNative[count];
            var prefsNative = MapPreferences(prefs);

            for (var i = 0; i < count; i++)
                inputs[i] = MapProperty(properties[i]);

            NativeScoringInterop.ScorePropertyBatch(inputs, count, in prefsNative, outputs);
            return outputs;
        }

        internal static void LogAvailability(ILogger logger)
        {
            if (_available)
                logger.LogInformation(
                    "Native scoring kernel loaded. Batch scoring will use the C++ fast path.");
            else
                logger.LogInformation(
                    "Native scoring kernel unavailable; using managed scoring implementation.");
        }

        private static PropertyInputNative MapProperty(Property p)
        {
            return new PropertyInputNative
            {
                Price = (double)p.Price,
                PropertyTax = (double)p.PropertyTax,
                HoaFee = (double)p.HoaFee,
                Utilities = (double)p.Utilities,
                LotSqft = p.LotSqft,
                Bedrooms = p.Bedrooms,
                Bathrooms = p.Bathrooms,
                CommuteMin = p.CommuteMin,
                SchoolRating = p.SchoolRating,
                Walkability = p.Walkability,
                TransitAccess = p.TransitAccess,
                CrimeScore = p.CrimeScore,
                FutureAppreciation = p.FutureAppreciation,
                ResalePotential = p.ResalePotential,
                RoofCondition = p.RoofCondition,
                AcCondition = p.AcCondition,
                PlumbingCondition = p.PlumbingCondition,
                ElectricalCondition = p.ElectricalCondition,
                // 0 is the sentinel for "unknown / never renovated" on the C++ side
                LastRenovationYear = p.LastRenovation ?? 0,
                AmenitiesScore = p.AmenitiesScore,
                FloodRisk = p.FloodRisk,
                NoiseLevel = p.NoiseLevel,
                Latitude = p.Latitude,
                Longitude = p.Longitude
            };
        }

        private static PreferencesInputNative MapPreferences(HomeSearchPreferencesDto prefs)
        {
            return new PreferencesInputNative
            {
                MaxPrice = (double)prefs.MaxPrice,
                MaxMonthlyBudget = (double)prefs.MaxMonthlyBudget,
                MinBedrooms = prefs.MinBedrooms,
                MinSqft = prefs.MinSqft,
                MaxCommuteMin = prefs.MaxCommuteMin,
                WAffordability = prefs.WeightAffordability,
                WNeighborhood = prefs.WeightNeighborhood,
                WSize = prefs.WeightSize,
                WAppreciation = prefs.WeightAppreciation,
                WCondition = prefs.WeightCondition,
                WCommute = prefs.WeightCommute,
                WAmenities = prefs.WeightAmenities,
                WTaxUtilities = prefs.WeightTaxUtilities,
                WResale = prefs.WeightResale,
                WEnvironment = prefs.WeightEnvironment,
                // Pass the current UTC year so the kernel is deterministic in tests.
                CurrentYear = DateTime.UtcNow.Year
            };
        }
    }
}
