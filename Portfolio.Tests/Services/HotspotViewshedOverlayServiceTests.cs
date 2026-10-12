using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    public class HotspotViewshedOverlayServiceTests
    {
        // ── Hotspot Clusterer (DBSCAN) ──────────────────────────────────────────

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

        // ── Viewshed Analyzer ───────────────────────────────────────────────────

        [Fact]
        public async Task Viewshed_FlatTerrain_AllCellsVisible()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = new ViewshedRequestDto
            {
                Width = 4,
                Height = 4,
                CellSize = 30,
                ObserverX = 0,
                ObserverY = 0,
                ObserverHeight = 2,
                Elevation = Enumerable.Repeat(100.0, 16).ToList()
            };

            var result = await service.ComputeAsync(request);

            Assert.Equal(16, result.VisibleCells);
            Assert.All(result.Visibility, v => Assert.Equal(1, v));
        }

        [Fact]
        public async Task Viewshed_PeakBlocksCellsBehindIt()
        {
            // 1x5 row, observer at x=0, a tall peak at x=1 blocks x=2,3,4.
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = new ViewshedRequestDto
            {
                Width = 5,
                Height = 1,
                CellSize = 1,
                ObserverX = 0,
                ObserverY = 0,
                ObserverHeight = 0,
                Elevation = [0, 10, 0, 0, 0]
            };

            var result = await service.ComputeAsync(request);

            Assert.Equal(1, result.Visibility[0]); // observer
            Assert.Equal(1, result.Visibility[1]); // the peak itself
            Assert.Equal(0, result.Visibility[2]); // behind the peak
            Assert.Equal(0, result.Visibility[3]);
            Assert.Equal(0, result.Visibility[4]);
            Assert.Equal(2, result.VisibleCells);
        }

        [Fact]
        public async Task Viewshed_ObserverCellAlwaysVisible()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = new ViewshedRequestDto
            {
                Width = 3,
                Height = 3,
                CellSize = 30,
                ObserverX = 1,
                ObserverY = 1,
                ObserverHeight = 0,
                Elevation = [0, 0, 0, 0, 0, 0, 0, 0, 0]
            };

            var result = await service.ComputeAsync(request);

            Assert.Equal(1, result.Visibility[1 * 3 + 1]);
        }

        [Fact]
        public async Task Viewshed_NullRequest_ThrowsArgumentNullException()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.ComputeAsync(null!));
        }

        [Fact]
        public async Task Viewshed_ObserverOutOfBounds_ThrowsArgumentException()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = new ViewshedRequestDto
            {
                Width = 2, Height = 2, CellSize = 30,
                ObserverX = 5, ObserverY = 0,
                Elevation = [1, 2, 3, 4]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeAsync(request));
        }

        [Fact]
        public async Task Viewshed_ElevationLengthMismatch_ThrowsArgumentException()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = new ViewshedRequestDto
            {
                Width = 2, Height = 2, CellSize = 30,
                ObserverX = 0, ObserverY = 0,
                Elevation = [1, 2, 3]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeAsync(request));
        }

        [Fact]
        public async Task Viewshed_ZeroCellSize_ThrowsArgumentException()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = new ViewshedRequestDto
            {
                Width = 2, Height = 2, CellSize = 0,
                ObserverX = 0, ObserverY = 0,
                Elevation = [1, 2, 3, 4]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeAsync(request));
        }

        // ── Viewshed caps and finite validation (WP-06) ─────────────────────────

        private static ViewshedRequestDto FlatViewshed(int width, int height, int observerX = 0, int observerY = 0) => new()
        {
            Width = width, Height = height, CellSize = 30,
            ObserverX = observerX, ObserverY = observerY,
            Elevation = Enumerable.Repeat(0.0, width * height).ToList()
        };

        [Theory]
        [InlineData(513, 1)]
        [InlineData(1, 513)]
        [InlineData(250000, 1)]
        public async Task Viewshed_SideOverCap_ThrowsArgumentException(int width, int height)
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeAsync(FlatViewshed(width, height)));
            Assert.StartsWith("Viewshed width and height are each limited to 512 cells.", ex.Message);
        }

        [Fact]
        public async Task Viewshed_WidthOf250000_IsRejectedQuickly()
        {
            // findings-security 5.2: 237 s of CPU before the side cap.
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = FlatViewshed(250000, 1);
            var timer = System.Diagnostics.Stopwatch.StartNew();

            await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeAsync(request));

            Assert.True(timer.ElapsedMilliseconds < 1000, $"took {timer.ElapsedMilliseconds} ms");
        }

        [Theory]
        [InlineData(512, 488)]
        [InlineData(40, 40)]
        [InlineData(512, 1)]
        public async Task Viewshed_AtOrBelowBoundary_Succeeds(int width, int height)
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);

            var result = await service.ComputeAsync(FlatViewshed(width, height, width / 2, height / 2));

            Assert.Equal(width * height, result.TotalCells);
            Assert.Equal(width * height, result.VisibleCells);
        }

        [Fact]
        public async Task Viewshed_CellCountMessageStillWinsOverSideCap()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = FlatViewshed(1, 1);
            request.Width = 300000;

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeAsync(request));
            Assert.Equal("Viewshed operations are limited to 250000 cells.", ex.Message);
        }

        [Fact]
        public async Task Viewshed_NonFiniteElevation_ThrowsArgumentException()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = FlatViewshed(3, 3, 1, 1);
            request.Elevation[8] = double.NaN;

            await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeAsync(request));
        }

        [Fact]
        public async Task Viewshed_CancelledToken_ThrowsOperationCanceled()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ComputeAsync(FlatViewshed(64, 64, 32, 32), cts.Token));
        }

        // ── DBSCAN seed de-duplication (WP-06) ──────────────────────────────────

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
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var labels = RunManagedDbscan(request);
            timer.Stop();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.All(labels, label => Assert.Equal(0, label));
            Assert.True(allocated < 32L * 1024 * 1024, $"allocated {allocated} bytes");
            Assert.True(timer.ElapsedMilliseconds < 1000, $"took {timer.ElapsedMilliseconds} ms");
        }

        [Fact]
        public async Task Dbscan_5000IdenticalPoints_ServiceFormsOneCluster()
        {
            var service = new SpatialClusterService(NullLogger<SpatialClusterService>.Instance);

            var result = await service.RunDbscanAsync(IdenticalPoints(5000));

            Assert.Equal(1, result.ClusterCount);
            Assert.Equal([5000], result.ClusterSizes);
        }

        // ── Spatial Overlay / Zone Tagger ───────────────────────────────────────

        [Fact]
        public async Task SpatialJoin_PointsAssignedToContainingZone()
        {
            var service = new SpatialOverlayService(NullLogger<SpatialOverlayService>.Instance);
            var request = new SpatialJoinRequestDto
            {
                Zones =
                [
                    new() { Name = "SW", Ring = Rect(0, 0, 0.5, 0.5) },
                    new() { Name = "NE", Ring = Rect(0.5, 0.5, 1.0, 1.0) }
                ],
                Points =
                [
                    new() { X = 0.25, Y = 0.25 }, // SW
                    new() { X = 0.75, Y = 0.75 }, // NE
                    new() { X = 0.9, Y = 0.1 }    // outside both
                ]
            };

            var result = await service.SpatialJoinAsync(request);

            Assert.Equal(0, result.Points[0].ZoneIndex);
            Assert.Equal(1, result.Points[1].ZoneIndex);
            Assert.Equal(-1, result.Points[2].ZoneIndex);
            Assert.Equal(2, result.AssignedCount);
            Assert.Equal(1, result.UnassignedCount);
            Assert.Equal(1, result.Zones[0].PointCount);
            Assert.Equal(1, result.Zones[1].PointCount);
        }

        [Fact]
        public async Task SpatialJoin_OverlappingZones_FirstMatchWins()
        {
            var service = new SpatialOverlayService(NullLogger<SpatialOverlayService>.Instance);
            var request = new SpatialJoinRequestDto
            {
                Zones =
                [
                    new() { Name = "Big", Ring = Rect(0, 0, 1, 1) },
                    new() { Name = "Small", Ring = Rect(0, 0, 0.5, 0.5) }
                ],
                Points = [new() { X = 0.25, Y = 0.25 }]
            };

            var result = await service.SpatialJoinAsync(request);

            Assert.Equal(0, result.Points[0].ZoneIndex); // first zone wins even though both contain it
        }

        [Fact]
        public async Task SpatialJoin_NullRequest_ThrowsArgumentNullException()
        {
            var service = new SpatialOverlayService(NullLogger<SpatialOverlayService>.Instance);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.SpatialJoinAsync(null!));
        }

        [Fact]
        public async Task SpatialJoin_EmptyPoints_ThrowsArgumentException()
        {
            var service = new SpatialOverlayService(NullLogger<SpatialOverlayService>.Instance);
            var request = new SpatialJoinRequestDto { Points = [], Zones = [new() { Name = "Z", Ring = Rect(0, 0, 1, 1) }] };
            await Assert.ThrowsAsync<ArgumentException>(() => service.SpatialJoinAsync(request));
        }

        [Fact]
        public async Task SpatialJoin_EmptyZones_ThrowsArgumentException()
        {
            var service = new SpatialOverlayService(NullLogger<SpatialOverlayService>.Instance);
            var request = new SpatialJoinRequestDto { Points = [new() { X = 0, Y = 0 }], Zones = [] };
            await Assert.ThrowsAsync<ArgumentException>(() => service.SpatialJoinAsync(request));
        }

        [Fact]
        public async Task SpatialJoin_ZoneWithTooFewVertices_ThrowsArgumentException()
        {
            var service = new SpatialOverlayService(NullLogger<SpatialOverlayService>.Instance);
            var request = new SpatialJoinRequestDto
            {
                Points = [new() { X = 0, Y = 0 }],
                Zones = [new() { Name = "Bad", Ring = [new() { X = 0, Y = 0 }, new() { X = 1, Y = 0 }] }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.SpatialJoinAsync(request));
        }

        [Fact]
        public async Task SpatialJoin_NonFinitePoint_ThrowsArgumentException()
        {
            var service = new SpatialOverlayService(NullLogger<SpatialOverlayService>.Instance);
            var request = new SpatialJoinRequestDto
            {
                Points = [new() { X = double.PositiveInfinity, Y = 0 }],
                Zones = [new() { Name = "Z", Ring = Rect(0, 0, 1, 1) }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.SpatialJoinAsync(request));
        }

        // ── DBSCAN: managed labels versus the pre-dedup algorithm ───────────────

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

        private static List<OverlayPointDto> Rect(double minX, double minY, double maxX, double maxY) =>
        [
            new() { X = minX, Y = minY },
            new() { X = maxX, Y = minY },
            new() { X = maxX, Y = maxY },
            new() { X = minX, Y = maxY }
        ];
    }
}
