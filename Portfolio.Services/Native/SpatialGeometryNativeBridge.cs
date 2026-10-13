using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;

namespace Portfolio.Services.Native
{
    internal static class SpatialGeometryNativeBridge
    {
        private static readonly bool _available = NativeKernelLoader.TryLoad("spatial_geometry_kernel");

        internal static bool IsAvailable => _available;

        internal static void LogAvailability(ILogger logger)
        {
            logger.LogInformation(_available
                ? "Native spatial geometry kernel loaded. Geometry operations will use the C++ fast path."
                : "Native spatial geometry kernel unavailable; using managed geometry implementation.");
        }

        internal static bool TryTriangulate(
            GeometryPointSetDto request,
            ILogger logger,
            out TriangulationResultDto? result)
        {
            result = null;
            if (!_available)
                return false;

            try
            {
                result = Triangulate(request);
                return true;
            }
            catch (Exception ex) when (NativeKernelLoader.IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native spatial geometry triangulation failed; falling back to managed geometry implementation.");
                return false;
            }
        }

        internal static bool TryClipToBoundingBox(
            PolygonClipRequestDto request,
            ILogger logger,
            out PolygonOperationResultDto? result)
        {
            result = null;
            if (!_available)
                return false;

            try
            {
                result = ClipToBoundingBox(request);
                return true;
            }
            catch (Exception ex) when (NativeKernelLoader.IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native spatial geometry clipping failed; falling back to managed geometry implementation.");
                return false;
            }
        }

        internal static TriangulationResultDto Triangulate(GeometryPointSetDto request)
        {
            var points = request.Points.Select(MapCoordinate).ToArray();
            var triangles = new TriangleNative[Math.Max(1, points.Length - 2)];
            var status = SpatialGeometryNativeInterop.TriangulateFan(points, points.Length, triangles, triangles.Length, out var triangleCount);
            ThrowIfFailed(status);

            return new TriangulationResultDto
            {
                NativeAccelerated = true,
                Triangles = triangles.Take(triangleCount).Select(MapTriangle).ToList()
            };
        }

        internal static PolygonOperationResultDto ClipToBoundingBox(PolygonClipRequestDto request)
        {
            var points = request.Subject.Select(MapCoordinate).ToArray();
            var bounds = new BoundingBoxNative
            {
                MinX = request.MinX,
                MinY = request.MinY,
                MaxX = request.MaxX,
                MaxY = request.MaxY
            };
            var output = new CoordinateNative[Math.Max(1, points.Length * 2)];
            var status = SpatialGeometryNativeInterop.ClipToBoundingBox(points, points.Length, in bounds, output, output.Length, out var outputCount);
            ThrowIfFailed(status);

            return new PolygonOperationResultDto
            {
                NativeAccelerated = true,
                Vertices = output.Take(outputCount).Select(MapCoordinate).ToList()
            };
        }

        private static CoordinateNative MapCoordinate(CoordinateDto dto)
        {
            return new CoordinateNative { X = dto.X, Y = dto.Y };
        }

        private static CoordinateDto MapCoordinate(CoordinateNative native)
        {
            return new CoordinateDto { X = native.X, Y = native.Y };
        }

        private static TriangleDto MapTriangle(TriangleNative native)
        {
            return new TriangleDto
            {
                A = MapCoordinate(native.A),
                B = MapCoordinate(native.B),
                C = MapCoordinate(native.C)
            };
        }

        private static void ThrowIfFailed(int status)
        {
            if (status != 0)
                throw new InvalidOperationException($"Native spatial geometry kernel failed with status {status}.");
        }
    }
}
