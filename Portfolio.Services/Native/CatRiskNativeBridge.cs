using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class CatRiskNativeBridge
    {
        private static readonly bool _available;

        static CatRiskNativeBridge()
        {
            try
            {
                if (NativeToggle.Disabled)
                {
                    _available = false;
                    return;
                }

                _available = NativeLibrary.TryLoad(
                    "cat_risk_kernel",
                    typeof(CatRiskNativeBridge).Assembly,
                    DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory,
                    out _);
            }
            catch
            {
                _available = false;
            }
        }

        internal static bool IsAvailable => _available;

        internal static void LogAvailability(ILogger logger)
        {
            logger.LogInformation(_available
                ? "Native catastrophe risk kernel loaded. Accumulation and simulation will use the C++ fast path."
                : "Native catastrophe risk kernel unavailable; using managed catastrophe risk implementation.");
        }

        internal static bool TryComputeRingAccumulation(
            AccumulationRequestDto request,
            ILogger logger,
            out double[]? ringTiv,
            out int[]? neighborCount)
        {
            ringTiv = null;
            neighborCount = null;
            if (!_available)
                return false;

            try
            {
                var locations = MapLocations(request.Locations);
                var tivOutput = new double[locations.Length];
                var neighborOutput = new int[locations.Length];

                var status = CatRiskNativeInterop.ComputeRingAccumulation(
                    locations,
                    locations.Length,
                    request.RadiusKm,
                    tivOutput,
                    neighborOutput,
                    tivOutput.Length);

                if (status < 0)
                    throw new InvalidOperationException($"Native catastrophe risk kernel failed with status {status}.");

                ringTiv = tivOutput;
                neighborCount = neighborOutput;
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native ring accumulation failed; falling back to managed implementation.");
                return false;
            }
        }

        internal static bool TrySimulateEventLosses(
            SimulationRequestDto request,
            ILogger logger,
            out double[]? eventLosses,
            out int[]? affectedCounts)
        {
            eventLosses = null;
            affectedCounts = null;
            if (!_available)
                return false;

            try
            {
                var locations = MapLocations(request.Locations);
                var events = new CatEventNative[request.Events.Count];
                for (var i = 0; i < request.Events.Count; i++)
                {
                    var e = request.Events[i];
                    events[i] = new CatEventNative
                    {
                        Latitude = e.Latitude,
                        Longitude = e.Longitude,
                        Intensity = e.Intensity,
                        RadiusKm = e.RadiusKm,
                        AnnualRate = e.AnnualRate
                    };
                }

                var lossOutput = new double[events.Length];
                var affectedOutput = new int[events.Length];

                var status = CatRiskNativeInterop.SimulateEventLosses(
                    locations,
                    locations.Length,
                    events,
                    events.Length,
                    request.VulnerabilityAlpha,
                    lossOutput,
                    affectedOutput,
                    lossOutput.Length);

                if (status < 0)
                    throw new InvalidOperationException($"Native catastrophe risk kernel failed with status {status}.");

                eventLosses = lossOutput;
                affectedCounts = affectedOutput;
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native loss simulation failed; falling back to managed implementation.");
                return false;
            }
        }

        private static CatLocationNative[] MapLocations(List<CatLocationDto> locations)
        {
            var mapped = new CatLocationNative[locations.Count];
            for (var i = 0; i < locations.Count; i++)
            {
                var l = locations[i];
                mapped[i] = new CatLocationNative
                {
                    Latitude = l.Latitude,
                    Longitude = l.Longitude,
                    InsuredValue = l.InsuredValue,
                    SiteHazard = l.SiteHazard,
                    DeductibleRate = l.DeductibleRate,
                    LimitRate = l.LimitRate
                };
            }
            return mapped;
        }

        private static bool IsNativeInvocationException(Exception exception)
        {
            return exception is DllNotFoundException
                or EntryPointNotFoundException
                or BadImageFormatException
                or SEHException
                or InvalidOperationException;
        }
    }
}
