using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;

namespace Portfolio.Services.Native
{
    internal static class SpatialClusterNativeBridge
    {
        private static readonly bool _available = NativeKernelLoader.TryLoad("spatial_cluster_kernel");

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

                ThrowIfFailed(status);

                labels = output;
                clusterCount = status;
                return true;
            }
            catch (Exception ex) when (NativeKernelLoader.IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native DBSCAN failed; falling back to managed clustering implementation.");
                return false;
            }
        }

        // A non-negative return is the cluster count. A negative one is a failure of the call itself, never
        // a property of the data (the service has validated the request and the output buffer is sized from
        // the point count), so it is surfaced as an InvalidOperationException: TryRunDbscan's catch logs it
        // and falls back to the managed DBSCAN, which has already seen the same validated request.
        //   -1  invalid argument (null pointer, count <= 0, short buffer, epsilon not finite and > 0, minPoints < 1)
        //  -99  C++ exception caught at the native boundary
        // internal so the status mapping can be tested without provoking a real native failure.
        internal static void ThrowIfFailed(int status)
        {
            if (status < 0)
                throw new InvalidOperationException($"Native spatial cluster kernel failed with status {status} ({DescribeStatus(status)}).");
        }

        private static string DescribeStatus(int status)
        {
            return status switch
            {
                -1 => "invalid argument",
                -99 => "native exception",
                _ => "unknown"
            };
        }
    }
}
