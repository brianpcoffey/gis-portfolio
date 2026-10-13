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

        // The managed path is a deterministic fan triangulation from the first point.
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

            // Sutherland–Hodgman: clip the subject polygon against each of the four box
            // half-planes in turn, in the same order as the native kernel so the two paths agree.
            // Unlike per-vertex clamping this computes true edge/box intersection points, emits box-corner
            // vertices, and yields an EMPTY polygon when the subject lies entirely outside the box.
            var poly = request.Subject.Select(p => new CoordinateDto { X = p.X, Y = p.Y }).ToList();
            poly = ClipHalfPlane(poly, p => p.X >= request.MinX, (a, b) => IntersectX(a, b, request.MinX));
            poly = ClipHalfPlane(poly, p => p.X <= request.MaxX, (a, b) => IntersectX(a, b, request.MaxX));
            poly = ClipHalfPlane(poly, p => p.Y >= request.MinY, (a, b) => IntersectY(a, b, request.MinY));
            poly = ClipHalfPlane(poly, p => p.Y <= request.MaxY, (a, b) => IntersectY(a, b, request.MaxY));

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new PolygonOperationResultDto
            {
                NativeAccelerated = false,
                Vertices = poly
            });
        }

        private static List<CoordinateDto> ClipHalfPlane(
            List<CoordinateDto> polygon,
            Func<CoordinateDto, bool> inside,
            Func<CoordinateDto, CoordinateDto, CoordinateDto> intersect)
        {
            var output = new List<CoordinateDto>();
            if (polygon.Count == 0)
                return output;

            for (var i = 0; i < polygon.Count; i++)
            {
                var current = polygon[i];
                var previous = polygon[(i - 1 + polygon.Count) % polygon.Count];
                var currentInside = inside(current);
                var previousInside = inside(previous);

                if (currentInside)
                {
                    if (!previousInside)
                        output.Add(intersect(previous, current)); // entering the box
                    output.Add(current);
                }
                else if (previousInside)
                {
                    output.Add(intersect(previous, current)); // leaving the box
                }
            }

            return output;
        }

        // Intersection of segment a→b with the vertical line X = x (a.X != b.X guaranteed
        // by the caller: it is only invoked when the endpoints straddle the boundary).
        private static CoordinateDto IntersectX(CoordinateDto a, CoordinateDto b, double x)
        {
            var t = (x - a.X) / (b.X - a.X);
            return new CoordinateDto { X = x, Y = a.Y + t * (b.Y - a.Y) };
        }

        private static CoordinateDto IntersectY(CoordinateDto a, CoordinateDto b, double y)
        {
            var t = (y - a.Y) / (b.Y - a.Y);
            return new CoordinateDto { X = a.X + t * (b.X - a.X), Y = y };
        }

        private static void ValidateCoordinates(IEnumerable<CoordinateDto> coordinates)
        {
            if (coordinates.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
                throw new ArgumentException("Coordinates must be finite numeric values.");
        }
    }
}
