using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class SpatialClusterNativeBridge
    {
        private static readonly bool _available;

        static SpatialClusterNativeBridge()
        {
            try
            {
                _available = NativeLibrary.TryLoad(
                    "spatial_cluster_kernel",
                    typeof(SpatialClusterNativeBridge).Assembly,
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
                ? "Native spatial cluster kernel loaded. DBSCAN will use the C++ fast path."
                : "Native spatial cluster kernel unavailable; using managed DBSCAN implementation.");
        }

        internal static bool TryRunDbscan(
            DbscanRequestDto request,
            ILogger logger,
            out int[]? labels,
            out int clusterCount)
        {
            labels = null;
            clusterCount = 0;
            if (!_available)
                return false;

            try
            {
                var points = request.Points
                    .Select(p => new ClusterPointNative { X = p.X, Y = p.Y })
                    .ToArray();
                var output = new int[points.Length];
                var status = SpatialClusterNativeInterop.RunDbscan(
                    points,
                    points.Length,
                    request.Epsilon,
                    request.MinPoints,
                    output,
                    output.Length);

                if (status < 0)
                    throw new InvalidOperationException($"Native spatial cluster kernel failed with status {status}.");

                labels = output;
                clusterCount = status;
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native DBSCAN failed; falling back to managed clustering implementation.");
                return false;
            }
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
