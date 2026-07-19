using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Native;

namespace Portfolio.Services.Services
{
    public class SpatialClusterService : ISpatialClusterService
    {
        private const int MaxPoints = 5000;
        private const int Unvisited = -2;
        private const int Noise = -1;

        private readonly ILogger<SpatialClusterService> _logger;

        public SpatialClusterService(ILogger<SpatialClusterService> logger)
        {
            _logger = logger;
            SpatialClusterNativeBridge.LogAvailability(_logger);
        }

        // Clusters points with DBSCAN, preferring the native kernel and falling back to managed code.
        public Task<DbscanResultDto> RunDbscanAsync(
            DbscanRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.Points is null || request.Points.Count == 0)
                throw new ArgumentException("At least one point is required.", nameof(request));
            if (request.Points.Count > MaxPoints)
                throw new ArgumentException($"Clustering is limited to {MaxPoints} points.", nameof(request));
            if (request.Epsilon <= 0 || double.IsNaN(request.Epsilon) || double.IsInfinity(request.Epsilon))
                throw new ArgumentException("Epsilon must be a finite value greater than zero.", nameof(request));
            if (request.MinPoints < 1)
                throw new ArgumentException("MinPoints must be at least 1.", nameof(request));
            foreach (var point in request.Points)
            {
                if (!IsFinite(point.X) || !IsFinite(point.Y))
                    throw new ArgumentException("Point coordinates must be finite values.", nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            bool nativeAccelerated;
            int[] labels;
            if (SpatialClusterNativeBridge.TryRunDbscan(request, _logger, out var nativeLabels, out _))
            {
                labels = nativeLabels!;
                nativeAccelerated = true;
            }
            else
            {
                labels = RunDbscanManaged(request, cancellationToken);
                nativeAccelerated = false;
            }

            return Task.FromResult(BuildResult(request, labels, nativeAccelerated));
        }

        private static int[] RunDbscanManaged(DbscanRequestDto request, CancellationToken cancellationToken)
        {
            var points = request.Points;
            var count = points.Count;
            var labels = new int[count];
            Array.Fill(labels, Unvisited);

            var epsilonSquared = request.Epsilon * request.Epsilon;
            var clusterId = 0;
            var neighbors = new List<int>();
            var seeds = new List<int>();

            for (var p = 0; p < count; p++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (labels[p] != Unvisited)
                    continue;

                RegionQuery(points, p, epsilonSquared, neighbors);
                if (neighbors.Count < request.MinPoints)
                {
                    labels[p] = Noise;
                    continue;
                }

                labels[p] = clusterId;
                seeds.Clear();
                seeds.AddRange(neighbors);
                for (var s = 0; s < seeds.Count; s++)
                {
                    var q = seeds[s];
                    if (labels[q] == Noise)
                        labels[q] = clusterId; // border point reclaimed from noise
                    if (labels[q] != Unvisited)
                        continue;

                    labels[q] = clusterId;
                    RegionQuery(points, q, epsilonSquared, neighbors);
                    if (neighbors.Count >= request.MinPoints)
                        seeds.AddRange(neighbors);
                }

                clusterId++;
            }

            return labels;
        }

        private static void RegionQuery(List<ClusterPointDto> points, int origin, double epsilonSquared, List<int> neighbors)
        {
            neighbors.Clear();
            var ox = points[origin].X;
            var oy = points[origin].Y;
            for (var i = 0; i < points.Count; i++)
            {
                var dx = points[i].X - ox;
                var dy = points[i].Y - oy;
                if (dx * dx + dy * dy <= epsilonSquared)
                    neighbors.Add(i);
            }
        }

        private static DbscanResultDto BuildResult(DbscanRequestDto request, int[] labels, bool nativeAccelerated)
        {
            var clusterCount = 0;
            foreach (var label in labels)
            {
                if (label >= clusterCount)
                    clusterCount = label + 1;
            }

            var clusterSizes = new int[clusterCount];
            var noiseCount = 0;
            var clusteredPoints = new List<ClusteredPointDto>(labels.Length);
            for (var i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == Noise)
                    noiseCount++;
                else if (label >= 0)
                    clusterSizes[label]++;

                clusteredPoints.Add(new ClusteredPointDto
                {
                    X = request.Points[i].X,
                    Y = request.Points[i].Y,
                    ClusterId = label < 0 ? Noise : label
                });
            }

            return new DbscanResultDto
            {
                NativeAccelerated = nativeAccelerated,
                ClusterCount = clusterCount,
                NoiseCount = noiseCount,
                ClusterSizes = clusterSizes.ToList(),
                Points = clusteredPoints
            };
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
