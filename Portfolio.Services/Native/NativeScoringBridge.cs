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
        // Loaded only when the library reports the ABI version this assembly was written against.
        private static readonly NativeLoadResult _load = NativeKernelLoader.Load(
            "portfolio_scoring", NativeScoringInterop.AbiVersion, NativeScoringInterop.ExpectedAbiVersion);

        private static readonly bool _available = _load == NativeLoadResult.Loaded;

        internal static bool IsAvailable => _available;

        // Caller MUST check IsAvailable before calling.
        // Throws InvalidOperationException if the kernel reports a non-zero status.
        internal static ScoreOutputNative ScoreProperty(
            Property p,
            HomeSearchPreferencesDto prefs)
        {
            var propNative = MapProperty(p);
            var prefsNative = MapPreferences(prefs);
            ThrowIfFailed(NativeScoringInterop.ScoreProperty(in propNative, in prefsNative, out var result, 1));
            return result;
        }

        // Returns a parallel array of ScoreOutputNative with the same element count.
        // Caller MUST check IsAvailable before calling.
        // Throws InvalidOperationException if the kernel reports a non-zero status.
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

            ThrowIfFailed(NativeScoringInterop.ScorePropertyBatch(inputs, count, in prefsNative, outputs, outputs.Length));
            return outputs;
        }

        // Runs the native batch call (score, ScorePropertyBatch in production) and reports a
        // native failure as false instead of throwing, logging a warning each time, so HomeScoringService can
        // fall back to its managed scoring like the other bridges' Try... wrappers. Only exceptions
        // NativeKernelLoader.IsNativeInvocationException classifies as native failures are absorbed.
        // The caller MUST check IsAvailable first; the delegate is a parameter so the fall-back can be
        // tested without a failing native library.
        internal static bool TryScorePropertyBatch(
            IReadOnlyList<Property> properties,
            HomeSearchPreferencesDto prefs,
            ILogger logger,
            Func<IReadOnlyList<Property>, HomeSearchPreferencesDto, ScoreOutputNative[]> score,
            out ScoreOutputNative[]? outputs)
        {
            try
            {
                outputs = score(properties, prefs);
                return true;
            }
            catch (Exception ex) when (NativeKernelLoader.IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native scoring failed; falling back to managed scoring.");
                outputs = null;
                return false;
            }
        }

        // Native status codes (portfolio_scoring.h): -1 invalid argument, -3 output buffer too small,
        // -99 C++ exception caught at the native boundary. The arrays are sized from the property count
        // here, so none of them can occur for well-formed input; they mean a bug, and are surfaced as an
        // InvalidOperationException rather than a value. That is the exception type
        // NativeKernelLoader.IsNativeInvocationException classifies as a native failure, so
        // TryScorePropertyBatch catches it and HomeScoringService falls back to managed scoring, as the
        // other bridges' Try... wrappers do.
        // internal so the status mapping can be tested without provoking a real native failure.
        internal static void ThrowIfFailed(int status)
        {
            if (status != 0)
                throw new InvalidOperationException($"Native scoring kernel failed with status {status} ({DescribeStatus(status)}).");
        }

        private static string DescribeStatus(int status)
        {
            return status switch
            {
                -1 => "invalid argument",
                -3 => "output buffer too small",
                -99 => "native exception",
                _ => "unknown"
            };
        }

        // Logs the availability the caller is going to use. HomeScoringService passes the value it was
        // constructed with, so its test constructor, which stands in for the library, does not report the
        // state of the real library next to results that never touched it.
        internal static void LogAvailability(ILogger logger, bool available)
        {
            if (available)
                logger.LogInformation(
                    "Native scoring kernel loaded. Batch scoring will use the C++ fast path.");
            else if (_load == NativeLoadResult.AbiMismatch)
                NativeKernelLoader.LogAbiMismatch(logger, "portfolio_scoring", NativeScoringInterop.ExpectedAbiVersion);
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
