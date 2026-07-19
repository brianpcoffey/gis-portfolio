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

        private static List<OverlayPointDto> Rect(double minX, double minY, double maxX, double maxY) =>
        [
            new() { X = minX, Y = minY },
            new() { X = maxX, Y = minY },
            new() { X = maxX, Y = maxY },
            new() { X = minX, Y = maxY }
        ];
    }
}
