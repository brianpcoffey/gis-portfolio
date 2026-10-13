using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    public class SpatialClusterServiceTests
    {
        [Fact]
        public async Task Dbscan_TwoDenseBlobs_FindsTwoClustersNoNoise()
        {
            var service = new SpatialClusterService(NullLogger<SpatialClusterService>.Instance);
            var request = new DbscanRequestDto
            {
                Epsilon = 1.0,
                MinPoints = 3,
                Points =
                [
                    new() { X = 0.0, Y = 0.0 }, new() { X = 0.1, Y = 0.0 }, new() { X = 0.0, Y = 0.1 },
                    new() { X = 0.1, Y = 0.1 }, new() { X = 0.2, Y = 0.0 },
                    new() { X = 10.0, Y = 10.0 }, new() { X = 10.1, Y = 10.0 }, new() { X = 10.0, Y = 10.1 },
                    new() { X = 10.1, Y = 10.1 }, new() { X = 10.2, Y = 10.0 }
                ]
            };

            var result = await service.RunDbscanAsync(request);

            Assert.Equal(2, result.ClusterCount);
            Assert.Equal(0, result.NoiseCount);
            Assert.Equal([5, 5], result.ClusterSizes);
        }

        [Fact]
        public async Task Dbscan_IsolatedPoints_AllNoise()
        {
            var service = new SpatialClusterService(NullLogger<SpatialClusterService>.Instance);
            var request = new DbscanRequestDto
            {
                Epsilon = 1.0,
                MinPoints = 2,
                Points = [new() { X = 0, Y = 0 }, new() { X = 5, Y = 5 }, new() { X = 10, Y = 10 }]
            };

            var result = await service.RunDbscanAsync(request);

            Assert.Equal(0, result.ClusterCount);
            Assert.Equal(3, result.NoiseCount);
            Assert.All(result.Points, p => Assert.Equal(-1, p.ClusterId));
        }

        [Fact]
        public async Task Dbscan_LabelsAlignWithInputOrder()
        {
            var service = new SpatialClusterService(NullLogger<SpatialClusterService>.Instance);
            var request = new DbscanRequestDto
            {
                Epsilon = 0.5,
                MinPoints = 3,
                Points =
                [
                    new() { X = 0, Y = 0 }, new() { X = 0.1, Y = 0 }, new() { X = 0, Y = 0.1 },
                    new() { X = 50, Y = 50 } // clear outlier
                ]
            };

            var result = await service.RunDbscanAsync(request);

            Assert.Equal(4, result.Points.Count);
            Assert.Equal(-1, result.Points[3].ClusterId);
            Assert.True(result.Points[0].ClusterId >= 0);
        }

        [Fact]
        public async Task Dbscan_NullRequest_ThrowsArgumentNullException()
        {
            var service = new SpatialClusterService(NullLogger<SpatialClusterService>.Instance);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.RunDbscanAsync(null!));
        }

        [Fact]
        public async Task Dbscan_EmptyPoints_ThrowsArgumentException()
        {
            var service = new SpatialClusterService(NullLogger<SpatialClusterService>.Instance);
            await Assert.ThrowsAsync<ArgumentException>(() => service.RunDbscanAsync(new DbscanRequestDto { Points = [] }));
        }

        [Fact]
        public async Task Dbscan_NonPositiveEpsilon_ThrowsArgumentException()
        {
            var service = new SpatialClusterService(NullLogger<SpatialClusterService>.Instance);
            var request = new DbscanRequestDto { Epsilon = 0, MinPoints = 2, Points = [new() { X = 0, Y = 0 }] };
            await Assert.ThrowsAsync<ArgumentException>(() => service.RunDbscanAsync(request));
        }

        [Fact]
        public async Task Dbscan_MinPointsBelowOne_ThrowsArgumentException()
        {
            var service = new SpatialClusterService(NullLogger<SpatialClusterService>.Instance);
            var request = new DbscanRequestDto { Epsilon = 1, MinPoints = 0, Points = [new() { X = 0, Y = 0 }] };
            await Assert.ThrowsAsync<ArgumentException>(() => service.RunDbscanAsync(request));
        }

        [Fact]
        public async Task Dbscan_NonFiniteCoordinate_ThrowsArgumentException()
        {
            var service = new SpatialClusterService(NullLogger<SpatialClusterService>.Instance);
            var request = new DbscanRequestDto
            {
                Epsilon = 1,
                MinPoints = 2,
                Points = [new() { X = double.NaN, Y = 0 }, new() { X = 0, Y = 0 }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.RunDbscanAsync(request));
        }

        private static DbscanRequestDto IdenticalPoints(int count) => new()
        {
            Epsilon = 1, MinPoints = 1,
            Points = Enumerable.Range(0, count).Select(_ => new ClusterPointDto { X = 3, Y = 4 }).ToList()
        };

        [Fact]
        public void Dbscan_5000IdenticalPoints_ManagedPathAllocatesUnder32Mb()
        {
            // findings-security 5.2: 313 MB allocated before seed de-duplication.
            var request = IdenticalPoints(5000);
            RunManagedDbscan(IdenticalPoints(10)); // warm up JIT outside the measurement

            var before = GC.GetAllocatedBytesForCurrentThread();
            var labels = RunManagedDbscan(request);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.All(labels, label => Assert.Equal(0, label));
            Assert.True(allocated < 32L * 1024 * 1024, $"allocated {allocated} bytes");
        }

        [Fact]
        public async Task Dbscan_5000IdenticalPoints_ServiceFormsOneCluster()
        {
            var service = new SpatialClusterService(NullLogger<SpatialClusterService>.Instance);

            var result = await service.RunDbscanAsync(IdenticalPoints(5000));

            Assert.Equal(1, result.ClusterCount);
            Assert.Equal([5000], result.ClusterSizes);
        }

        private static int[] RunManagedDbscan(DbscanRequestDto request)
        {
            var method = typeof(SpatialClusterService).GetMethod(
                "RunDbscanManaged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            return (int[])method.Invoke(null, [request, CancellationToken.None])!;
        }

        // Verbatim copy of the managed DBSCAN as it stood before seed de-duplication (WP-06).
        private static int[] ReferenceDbscan(DbscanRequestDto request)
        {
            const int unvisited = -2;
            const int noise = -1;
            var points = request.Points;
            var count = points.Count;
            var labels = new int[count];
            Array.Fill(labels, unvisited);

            var epsilonSquared = request.Epsilon * request.Epsilon;
            var clusterId = 0;
            var neighbors = new List<int>();
            var seeds = new List<int>();

            void RegionQuery(int origin)
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

            for (var p = 0; p < count; p++)
            {
                if (labels[p] != unvisited)
                    continue;

                RegionQuery(p);
                if (neighbors.Count < request.MinPoints)
                {
                    labels[p] = noise;
                    continue;
                }

                labels[p] = clusterId;
                seeds.Clear();
                seeds.AddRange(neighbors);
                for (var s = 0; s < seeds.Count; s++)
                {
                    var q = seeds[s];
                    if (labels[q] == noise)
                        labels[q] = clusterId;
                    if (labels[q] != unvisited)
                        continue;

                    labels[q] = clusterId;
                    RegionQuery(q);
                    if (neighbors.Count >= request.MinPoints)
                        seeds.AddRange(neighbors);
                }

                clusterId++;
            }

            return labels;
        }

        [Fact]
        public void Dbscan_ManagedLabels_MatchReferenceAlgorithmOnSeededRandomCases()
        {
            var random = new Random(20261012);
            for (var caseIndex = 0; caseIndex < 1000; caseIndex++)
            {
                var pointCount = random.Next(1, 80);
                // A coarse integer lattice yields many exact duplicates and dense overlap.
                var points = Enumerable.Range(0, pointCount)
                    .Select(_ => new ClusterPointDto { X = random.Next(0, 9), Y = random.Next(0, 9) })
                    .ToList();
                var request = new DbscanRequestDto
                {
                    Epsilon = 0.5 + random.NextDouble() * 2.5,
                    MinPoints = random.Next(1, 6),
                    Points = points
                };

                Assert.Equal(ReferenceDbscan(request), RunManagedDbscan(request));
            }
        }
    }
}
