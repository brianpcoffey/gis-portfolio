using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    public class SpatialGeometryServiceTests
    {
        [Fact]
        public async Task SpatialGeometryTriangulate_ValidPoints_ReturnsFanTriangles()
        {
            // Arrange
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new GeometryPointSetDto
            {
                Points =
                [
                    new CoordinateDto { X = 0, Y = 0 },
                    new CoordinateDto { X = 1, Y = 0 },
                    new CoordinateDto { X = 1, Y = 1 },
                    new CoordinateDto { X = 0, Y = 1 }
                ]
            };

            // Act
            var result = await service.TriangulateAsync(request);

            // Assert
            Assert.Equal(2, result.Triangles.Count);
        }

        [Fact]
        public async Task SpatialGeometryClip_PolygonStraddlingCorner_EmitsBoxCorner()
        {
            // A triangle whose right-angle overhangs the box's top-right corner. Correct
            // Sutherland–Hodgman clipping against [0,1]x[0,1] yields the quadrilateral
            // (0.5,0.5),(1,0.5),(1,1),(0.5,1) — crucially INCLUDING the (1,1) box corner
            // that naive per-vertex clamping drops.
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new PolygonClipRequestDto
            {
                MinX = 0, MinY = 0, MaxX = 1, MaxY = 1,
                Subject =
                [
                    new CoordinateDto { X = 0.5, Y = 0.5 },
                    new CoordinateDto { X = 2.0, Y = 0.5 },
                    new CoordinateDto { X = 0.5, Y = 2.0 }
                ]
            };

            var result = await service.ClipToBoundingBoxAsync(request);

            Assert.Equal(4, result.Vertices.Count);
            Assert.Contains(result.Vertices, v => Math.Abs(v.X - 1.0) < 1e-9 && Math.Abs(v.Y - 1.0) < 1e-9);
            Assert.All(result.Vertices, v =>
            {
                Assert.InRange(v.X, 0.0, 1.0);
                Assert.InRange(v.Y, 0.0, 1.0);
            });
        }

        [Fact]
        public async Task SpatialGeometryClip_PolygonFullyOutsideBox_ReturnsEmpty()
        {
            // A polygon with no overlap with the box must clip to nothing — not collapse
            // to duplicate corner points (the old clamp bug that falsely reported a shape).
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new PolygonClipRequestDto
            {
                MinX = 0, MinY = 0, MaxX = 1, MaxY = 1,
                Subject =
                [
                    new CoordinateDto { X = 5, Y = 5 },
                    new CoordinateDto { X = 6, Y = 5 },
                    new CoordinateDto { X = 5.5, Y = 6 }
                ]
            };

            var result = await service.ClipToBoundingBoxAsync(request);

            Assert.Empty(result.Vertices);
        }

        [Fact]
        public async Task SpatialGeometryTriangulate_NonFiniteCoordinate_ThrowsArgumentException()
        {
            // Arrange
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new GeometryPointSetDto
            {
                Points =
                [
                    new CoordinateDto { X = 0, Y = 0 },
                    new CoordinateDto { X = double.NaN, Y = 0 },
                    new CoordinateDto { X = 1, Y = 1 }
                ]
            };

            // Act / Assert
            await Assert.ThrowsAsync<ArgumentException>(() => service.TriangulateAsync(request));
        }

        // ── Geometry Toolkit validation ─────────────────────────────────────

        [Fact]
        public async Task SpatialGeometryTriangulate_NullRequest_ThrowsArgumentNullException()
        {
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.TriangulateAsync(null!));
        }

        [Fact]
        public async Task SpatialGeometryTriangulate_FewerThanThreePoints_ThrowsArgumentException()
        {
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new GeometryPointSetDto
            {
                Points = [new CoordinateDto { X = 0, Y = 0 }, new CoordinateDto { X = 1, Y = 1 }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.TriangulateAsync(request));
        }

        [Fact]
        public async Task SpatialGeometryTriangulate_NPointPolygon_ProducesNMinus2Triangles()
        {
            // A fan from origin: N points → N-2 triangles
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new GeometryPointSetDto
            {
                Points =
                [
                    new CoordinateDto { X = 0, Y = 0 },
                    new CoordinateDto { X = 1, Y = 0 },
                    new CoordinateDto { X = 1, Y = 1 },
                    new CoordinateDto { X = 0, Y = 1 },
                    new CoordinateDto { X = 0.5, Y = 1.5 }
                ]
            };

            var result = await service.TriangulateAsync(request);

            Assert.Equal(3, result.Triangles.Count);
        }

        [Fact]
        public async Task SpatialGeometryClip_NullRequest_ThrowsArgumentNullException()
        {
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.ClipToBoundingBoxAsync(null!));
        }

        [Fact]
        public async Task SpatialGeometryClip_InvalidBoundingBox_ThrowsArgumentException()
        {
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new PolygonClipRequestDto
            {
                MinX = 1, MaxX = 0, MinY = 0, MaxY = 1,   // min > max — invalid
                Subject = [new CoordinateDto { X = 0, Y = 0 }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ClipToBoundingBoxAsync(request));
        }

        [Fact]
        public async Task SpatialGeometryClip_InfiniteCoordinate_ThrowsArgumentException()
        {
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new PolygonClipRequestDto
            {
                MinX = 0, MaxX = 1, MinY = 0, MaxY = 1,
                Subject = [new CoordinateDto { X = double.PositiveInfinity, Y = 0 }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ClipToBoundingBoxAsync(request));
        }
    }
}
