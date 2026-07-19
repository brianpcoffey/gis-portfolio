using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class NetworkTraceNativeBridge
    {
        private static readonly bool _available;

        static NetworkTraceNativeBridge()
        {
            try
            {
                _available = NativeLibrary.TryLoad(
                    "network_trace_kernel",
                    typeof(NetworkTraceNativeBridge).Assembly,
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
                ? "Native network trace kernel loaded. Outage tracing will use the C++ fast path."
                : "Native network trace kernel unavailable; using managed network trace implementation.");
        }

        internal static bool TryDownstream(
            List<NetworkElementDto> elements,
            int sourceNodeId,
            int faultElementId,
            ILogger logger,
            out int[]? downstreamIds,
            out int customersAffected)
        {
            downstreamIds = null;
            customersAffected = 0;
            if (!_available)
                return false;

            try
            {
                var native = MapElements(elements);
                var output = new int[native.Length];

                var count = NetworkTraceNativeInterop.Downstream(
                    native,
                    native.Length,
                    sourceNodeId,
                    faultElementId,
                    output,
                    output.Length,
                    out var affected);

                if (count < 0)
                    throw new InvalidOperationException($"Native network trace kernel failed with status {count}.");

                downstreamIds = Take(output, count);
                customersAffected = affected;
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native downstream trace failed; falling back to managed implementation.");
                return false;
            }
        }

        internal static bool TryUpstream(
            List<NetworkElementDto> elements,
            int sourceNodeId,
            int faultElementId,
            ILogger logger,
            out int[]? upstreamIds)
        {
            upstreamIds = null;
            if (!_available)
                return false;

            try
            {
                var native = MapElements(elements);
                var output = new int[native.Length];

                var count = NetworkTraceNativeInterop.Upstream(
                    native,
                    native.Length,
                    sourceNodeId,
                    faultElementId,
                    output,
                    output.Length);

                if (count < 0)
                    throw new InvalidOperationException($"Native network trace kernel failed with status {count}.");

                upstreamIds = Take(output, count);
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native upstream trace failed; falling back to managed implementation.");
                return false;
            }
        }

        internal static bool TryFindIsolationDevices(
            List<NetworkElementDto> elements,
            int sourceNodeId,
            int faultElementId,
            ILogger logger,
            out int[]? deviceIds)
        {
            deviceIds = null;
            if (!_available)
                return false;

            try
            {
                var native = MapElements(elements);
                var output = new int[native.Length];

                var count = NetworkTraceNativeInterop.FindIsolationDevices(
                    native,
                    native.Length,
                    sourceNodeId,
                    faultElementId,
                    output,
                    output.Length);

                if (count < 0)
                    throw new InvalidOperationException($"Native network trace kernel failed with status {count}.");

                deviceIds = Take(output, count);
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native isolation search failed; falling back to managed implementation.");
                return false;
            }
        }

        internal static bool TryComputeEnergizedSet(
            List<NetworkElementDto> elements,
            int sourceNodeId,
            int[] overrideElementIds,
            int[] overrideStates,
            ILogger logger,
            out int[]? energizedIds,
            out int customersServed)
        {
            energizedIds = null;
            customersServed = 0;
            if (!_available)
                return false;

            try
            {
                var native = MapElements(elements);
                var output = new int[native.Length];

                var count = NetworkTraceNativeInterop.ComputeEnergizedSet(
                    native,
                    native.Length,
                    sourceNodeId,
                    overrideElementIds,
                    overrideStates,
                    overrideElementIds.Length,
                    output,
                    output.Length,
                    out var served);

                if (count < 0)
                    throw new InvalidOperationException($"Native network trace kernel failed with status {count}.");

                energizedIds = Take(output, count);
                customersServed = served;
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native energization sweep failed; falling back to managed implementation.");
                return false;
            }
        }

        private static TraceElementNative[] MapElements(List<NetworkElementDto> elements)
        {
            var mapped = new TraceElementNative[elements.Count];
            for (var i = 0; i < elements.Count; i++)
            {
                var e = elements[i];
                mapped[i] = new TraceElementNative
                {
                    Id = e.Id,
                    FromNodeId = e.FromNodeId,
                    ToNodeId = e.ToNodeId,
                    DeviceType = e.DeviceType,
                    IsOpen = e.IsOpen ? 1 : 0,
                    CustomerCount = e.CustomerCount
                };
            }
            return mapped;
        }

        private static int[] Take(int[] buffer, int count)
        {
            var result = new int[count];
            Array.Copy(buffer, result, count);
            return result;
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
