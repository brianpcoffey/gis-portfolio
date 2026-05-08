using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Native;

namespace Portfolio.Services.Services
{
    public class SpatialGeometryService : ISpatialGeometryService
    {
        private const int MaxPointCount = 5000;
        private readonly ILogger<SpatialGeometryService> _logger;

        public SpatialGeometryService(ILogger<SpatialGeometryService> logger)
        {
            _logger = logger;
            SpatialGeometryNativeBridge.LogAvailability(_logger);
        }

        // Builds a deterministic fan triangulation for map visualization; native code can replace the hot path.
        public Task<TriangulationResultDto> TriangulateAsync(
            GeometryPointSetDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.Points is null || request.Points.Count < 3)
                throw new ArgumentException("At least three points are required for triangulation.", nameof(request));
            if (request.Points.Count > MaxPointCount)
                throw new ArgumentException($"Geometry operations are limited to {MaxPointCount} points.", nameof(request));
            ValidateCoordinates(request.Points);

            cancellationToken.ThrowIfCancellationRequested();

            if (SpatialGeometryNativeBridge.TryTriangulate(request, _logger, out var nativeResult))
                return Task.FromResult(nativeResult!);

            var triangles = new List<TriangleDto>();
            var origin = request.Points[0];
            for (var i = 1; i < request.Points.Count - 1; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                triangles.Add(new TriangleDto
                {
                    A = origin,
                    B = request.Points[i],
                    C = request.Points[i + 1]
                });
            }

            return Task.FromResult(new TriangulationResultDto
            {
                NativeAccelerated = false,
                Triangles = triangles
            });
        }

        // Clips polygon vertices to a bounding box for a simple demonstrable geometry operation.
        public Task<PolygonOperationResultDto> ClipToBoundingBoxAsync(
            PolygonClipRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.Subject is null || request.Subject.Count == 0)
                throw new ArgumentException("At least one polygon vertex is required.", nameof(request));
            if (request.Subject.Count > MaxPointCount)
                throw new ArgumentException($"Geometry operations are limited to {MaxPointCount} points.", nameof(request));
            if (request.MinX >= request.MaxX || request.MinY >= request.MaxY)
                throw new ArgumentException("Bounding box minimums must be less than maximums.", nameof(request));
            ValidateCoordinates(request.Subject);

            cancellationToken.ThrowIfCancellationRequested();

            if (SpatialGeometryNativeBridge.TryClipToBoundingBox(request, _logger, out var nativeResult))
                return Task.FromResult(nativeResult!);

            var vertices = new List<CoordinateDto>(request.Subject.Count);
            foreach (var point in request.Subject)
            {
                cancellationToken.ThrowIfCancellationRequested();
                vertices.Add(new CoordinateDto
                {
                    X = Math.Clamp(point.X, request.MinX, request.MaxX),
                    Y = Math.Clamp(point.Y, request.MinY, request.MaxY)
                });
            }

            return Task.FromResult(new PolygonOperationResultDto
            {
                NativeAccelerated = false,
                Vertices = vertices
            });
        }

        private static void ValidateCoordinates(IEnumerable<CoordinateDto> coordinates)
        {
            if (coordinates.Any(p => double.IsNaN(p.X) || double.IsInfinity(p.X) || double.IsNaN(p.Y) || double.IsInfinity(p.Y)))
                throw new ArgumentException("Coordinates must be finite numeric values.");
        }
    }
}
