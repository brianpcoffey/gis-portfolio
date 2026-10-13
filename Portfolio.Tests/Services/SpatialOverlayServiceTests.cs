using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    public class SpatialOverlayServiceTests
    {
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
