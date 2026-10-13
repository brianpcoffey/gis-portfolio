using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;

namespace Portfolio.Services.Native
{
    internal static class SpatialOverlayNativeBridge
    {
        private static readonly bool _available = NativeKernelLoader.TryLoad("spatial_overlay_kernel");

        internal static bool IsAvailable => _available;

        internal static void LogAvailability(ILogger logger)
        {
            logger.LogInformation(_available
                ? "Native spatial overlay kernel loaded. Spatial joins will use the C++ fast path."
                : "Native spatial overlay kernel unavailable; using managed spatial-join implementation.");
        }

        internal static bool TryAssignPointsToZones(
            SpatialJoinRequestDto request,
            ILogger logger,
            out int[]? assignments)
        {
            assignments = null;
            if (!_available)
                return false;

            try
            {
                var points = request.Points
                    .Select(p => new OverlayPointNative { X = p.X, Y = p.Y })
                    .ToArray();

                var ringSizes = request.Zones.Select(z => z.Ring.Count).ToArray();
                var totalVertices = ringSizes.Sum();
                var polygonVertices = new OverlayPointNative[totalVertices];
                var index = 0;
                foreach (var zone in request.Zones)
                {
                    foreach (var vertex in zone.Ring)
                        polygonVertices[index++] = new OverlayPointNative { X = vertex.X, Y = vertex.Y };
                }

                var output = new int[points.Length];
                var status = SpatialOverlayNativeInterop.AssignPointsToZones(
                    points,
                    points.Length,
                    polygonVertices,
                    totalVertices,
                    ringSizes,
                    ringSizes.Length,
                    output,
                    output.Length);

                if (status < 0)
                    throw new InvalidOperationException($"Native spatial overlay kernel failed with status {status}.");

                assignments = output;
                return true;
            }
            catch (Exception ex) when (NativeKernelLoader.IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native spatial join failed; falling back to managed implementation.");
                return false;
            }
        }
    }
}
