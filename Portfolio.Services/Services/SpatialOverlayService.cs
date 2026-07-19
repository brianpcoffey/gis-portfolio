using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Native;

namespace Portfolio.Services.Services
{
    public class SpatialOverlayService : ISpatialOverlayService
    {
        private const int MaxPoints = 10000;
        private const int MaxZones = 200;
        private const int MaxTotalVertices = 20000;

        private readonly ILogger<SpatialOverlayService> _logger;

        public SpatialOverlayService(ILogger<SpatialOverlayService> logger)
        {
            _logger = logger;
            SpatialOverlayNativeBridge.LogAvailability(_logger);
        }

        // Assigns each point to its containing zone, preferring the native kernel and falling back to managed code.
        public Task<SpatialJoinResultDto> SpatialJoinAsync(
            SpatialJoinRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.Points is null || request.Points.Count == 0)
                throw new ArgumentException("At least one point is required.", nameof(request));
            if (request.Points.Count > MaxPoints)
                throw new ArgumentException($"Spatial joins are limited to {MaxPoints} points.", nameof(request));
            if (request.Zones is null || request.Zones.Count == 0)
                throw new ArgumentException("At least one zone is required.", nameof(request));
            if (request.Zones.Count > MaxZones)
                throw new ArgumentException($"Spatial joins are limited to {MaxZones} zones.", nameof(request));

            var totalVertices = 0;
            foreach (var zone in request.Zones)
            {
                if (zone.Ring is null || zone.Ring.Count < 3)
                    throw new ArgumentException("Each zone must have a ring of at least three vertices.", nameof(request));
                foreach (var vertex in zone.Ring)
                {
                    if (!IsFinite(vertex.X) || !IsFinite(vertex.Y))
                        throw new ArgumentException("Zone vertices must be finite values.", nameof(request));
                }
                totalVertices += zone.Ring.Count;
            }
            if (totalVertices > MaxTotalVertices)
                throw new ArgumentException($"Zones are limited to {MaxTotalVertices} total vertices.", nameof(request));

            foreach (var point in request.Points)
            {
                if (!IsFinite(point.X) || !IsFinite(point.Y))
                    throw new ArgumentException("Point coordinates must be finite values.", nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            bool nativeAccelerated;
            int[] assignments;
            if (SpatialOverlayNativeBridge.TryAssignPointsToZones(request, _logger, out var nativeAssignments))
            {
                assignments = nativeAssignments!;
                nativeAccelerated = true;
            }
            else
            {
                assignments = AssignManaged(request, cancellationToken);
                nativeAccelerated = false;
            }

            return Task.FromResult(BuildResult(request, assignments, nativeAccelerated));
        }

        private static int[] AssignManaged(SpatialJoinRequestDto request, CancellationToken cancellationToken)
        {
            var assignments = new int[request.Points.Count];
            for (var p = 0; p < request.Points.Count; p++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var point = request.Points[p];
                var matched = -1;
                for (var z = 0; z < request.Zones.Count; z++)
                {
                    if (PointInRing(request.Zones[z].Ring, point.X, point.Y))
                    {
                        matched = z;
                        break;
                    }
                }
                assignments[p] = matched;
            }
            return assignments;
        }

        // Even-odd (crossing-number) point-in-polygon test over a ring of vertices.
        private static bool PointInRing(List<OverlayPointDto> ring, double px, double py)
        {
            var inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                var xi = ring[i].X;
                var yi = ring[i].Y;
                var xj = ring[j].X;
                var yj = ring[j].Y;
                var crosses = ((yi > py) != (yj > py)) &&
                    (px < (xj - xi) * (py - yi) / (yj - yi) + xi);
                if (crosses)
                    inside = !inside;
            }
            return inside;
        }

        private static SpatialJoinResultDto BuildResult(SpatialJoinRequestDto request, int[] assignments, bool nativeAccelerated)
        {
            var zoneCounts = new int[request.Zones.Count];
            var assignedCount = 0;
            var taggedPoints = new List<TaggedPointDto>(assignments.Length);
            for (var i = 0; i < assignments.Length; i++)
            {
                var zoneIndex = assignments[i];
                if (zoneIndex >= 0 && zoneIndex < zoneCounts.Length)
                {
                    zoneCounts[zoneIndex]++;
                    assignedCount++;
                }

                taggedPoints.Add(new TaggedPointDto
                {
                    X = request.Points[i].X,
                    Y = request.Points[i].Y,
                    ZoneIndex = zoneIndex
                });
            }

            var zoneSummaries = new List<ZoneSummaryDto>(request.Zones.Count);
            for (var z = 0; z < request.Zones.Count; z++)
            {
                zoneSummaries.Add(new ZoneSummaryDto
                {
                    ZoneIndex = z,
                    Name = request.Zones[z].Name ?? string.Empty,
                    PointCount = zoneCounts[z]
                });
            }

            return new SpatialJoinResultDto
            {
                NativeAccelerated = nativeAccelerated,
                AssignedCount = assignedCount,
                UnassignedCount = assignments.Length - assignedCount,
                Zones = zoneSummaries,
                Points = taggedPoints
            };
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
