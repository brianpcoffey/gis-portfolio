using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    public class RasterTerrainServiceTests
    {
        [Fact]
        public async Task RasterTerrainHillshade_ValidRaster_ReturnsIntensityGrid()
        {
            // Arrange
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = new RasterHillshadeRequestDto
            {
                Width = 2,
                Height = 2,
                Elevation = [10, 20, 30, 40]
            };

            // Act
            var result = await service.GenerateHillshadeAsync(request);

            // Assert
            Assert.Equal(4, result.Intensities.Count);
        }

        [Fact]
        public async Task RasterTerrainHeatmap_ValidRequest_ReturnsNormalizedGrid()
        {
            // Arrange
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = new HeatmapRequestDto
            {
                Width = 2,
                Height = 2,
                MinX = 0,
                MinY = 0,
                MaxX = 1,
                MaxY = 1,
                Radius = 0.5,
                Points = [new WeightedPointDto { X = 0.5, Y = 0.5, Weight = 1 }]
            };

            // Act
            var result = await service.GenerateHeatmapAsync(request);

            // Assert
            Assert.Equal(4, result.Values.Count);
            Assert.All(result.Values, value => Assert.InRange(value, 0, 1));
        }

        [Fact]
        public async Task RasterTerrainHillshade_TooManyCells_ThrowsArgumentException()
        {
            // Arrange
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = new RasterHillshadeRequestDto
            {
                Width = 501,
                Height = 500,
                Elevation = Enumerable.Repeat(1.0, 250500).ToList()
            };

            // Act / Assert
            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHillshadeAsync(request));
        }

        [Fact]
        public async Task RasterTerrainHillshade_NullRequest_ThrowsArgumentNullException()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.GenerateHillshadeAsync(null!));
        }

        [Fact]
        public async Task RasterTerrainHillshade_ZeroCellSize_ThrowsArgumentException()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = new RasterHillshadeRequestDto
            {
                Width = 2,
                Height = 2,
                CellSize = 0,
                Elevation = [10, 20, 30, 40]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHillshadeAsync(request));
        }

        [Fact]
        public async Task RasterTerrainHillshade_ElevationLengthMismatch_ThrowsArgumentException()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = new RasterHillshadeRequestDto
            {
                Width = 2,
                Height = 2,
                CellSize = 1,
                Elevation = [10, 20, 30]   // should be 4
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHillshadeAsync(request));
        }

        [Fact]
        public async Task RasterTerrainHeatmap_NullRequest_ThrowsArgumentNullException()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.GenerateHeatmapAsync(null!));
        }

        [Fact]
        public async Task RasterTerrainHeatmap_InvalidExtent_ThrowsArgumentException()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = new HeatmapRequestDto
            {
                Width = 2,
                Height = 2,
                Radius = 0.5,
                MinX = 1,
                MaxX = 0,
                MinY = 0,
                MaxY = 1,   // min > max
                Points = [new WeightedPointDto { X = 0.5, Y = 0.5, Weight = 1 }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHeatmapAsync(request));
        }

        [Fact]
        public async Task RasterTerrainHeatmap_ZeroRadius_ThrowsArgumentException()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = new HeatmapRequestDto
            {
                Width = 2,
                Height = 2,
                Radius = 0,
                MinX = 0,
                MaxX = 1,
                MinY = 0,
                MaxY = 1,
                Points = [new WeightedPointDto { X = 0.5, Y = 0.5, Weight = 1 }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHeatmapAsync(request));
        }

        [Fact]
        public async Task RasterTerrainHillshade_1x1Raster_ReturnsSingleIntensity()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = new RasterHillshadeRequestDto
            {
                Width = 1,
                Height = 1,
                CellSize = 1,
                Elevation = [100.0]
            };

            var result = await service.GenerateHillshadeAsync(request);

            Assert.Single(result.Intensities);
            Assert.InRange(result.Intensities[0], (byte)0, (byte)255);
        }

        private static RasterHillshadeRequestDto Hillshade(int width, int height) => new()
        {
            Width = width,
            Height = height,
            Elevation = Enumerable.Repeat(10.0, width * height).ToList()
        };

        private static HeatmapRequestDto Heatmap(int width, int height, int pointCount) => new()
        {
            Width = width,
            Height = height,
            MinX = 0,
            MinY = 0,
            MaxX = 1,
            MaxY = 1,
            Radius = 0.1,
            Points = Enumerable.Range(0, pointCount)
                .Select(i => new WeightedPointDto { X = (i % 10) / 10.0, Y = (i % 7) / 7.0, Weight = 1 })
                .ToList()
        };

        [Fact]
        public async Task RasterTerrainHillshade_SideOverCap_ThrowsArgumentException()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHillshadeAsync(Hillshade(513, 1)));
            Assert.StartsWith("Raster width and height are each limited to 512 cells.", ex.Message);
        }

        [Fact]
        public async Task RasterTerrainHillshade_AtBoundary512x488_Succeeds()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);

            var result = await service.GenerateHillshadeAsync(Hillshade(512, 488));

            Assert.Equal(512 * 488, result.Intensities.Count);
        }

        [Fact]
        public async Task RasterTerrainHeatmap_SideOverCap_ThrowsArgumentException()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);

            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHeatmapAsync(Heatmap(513, 1, 1)));
        }

        [Fact]
        public async Task RasterTerrainHeatmap_WorkJustOverBudget_ThrowsArgumentException()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);

            // 500 x 500 x 101 = 25,250,000 > 25,000,000.
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHeatmapAsync(Heatmap(500, 500, 101)));
            Assert.StartsWith("Heatmaps are limited to 25000000 cell-point evaluations.", ex.Message);
        }

        [Fact]
        public async Task RasterTerrainHeatmap_WorkExactlyAtBudget_Succeeds()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);

            // 500 x 500 x 100 = 25,000,000.
            var result = await service.GenerateHeatmapAsync(Heatmap(500, 500, 100));

            Assert.Equal(250_000, result.Values.Count);
        }

        [Fact]
        public async Task RasterTerrainHeatmap_UiMaximum_Succeeds()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);

            var result = await service.GenerateHeatmapAsync(Heatmap(40, 40, 500));

            Assert.Equal(1600, result.Values.Count);
        }

        [Fact]
        public async Task RasterTerrainHeatmap_TenThousandPointsOn500x500_IsRejectedQuickly()
        {
            // Measured before the cap: this input took 29.2 s.
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = Heatmap(500, 500, 10_000);
            var timer = System.Diagnostics.Stopwatch.StartNew();

            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHeatmapAsync(request));

            Assert.True(timer.ElapsedMilliseconds < 5000, $"took {timer.ElapsedMilliseconds} ms");
        }

        [Fact]
        public async Task RasterTerrainHillshade_NonFiniteElevation_ThrowsArgumentException()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = Hillshade(2, 2);
            request.Elevation[3] = double.NaN;

            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHillshadeAsync(request));
        }

        [Theory]
        [InlineData(double.NaN, 45)]
        [InlineData(315, double.NaN)]
        [InlineData(double.PositiveInfinity, 45)]
        public async Task RasterTerrainHillshade_NonFiniteSun_ThrowsArgumentException(double azimuth, double altitude)
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = Hillshade(2, 2);
            request.AzimuthDegrees = azimuth;
            request.AltitudeDegrees = altitude;

            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHillshadeAsync(request));
        }

        [Theory]
        [InlineData("minx")]
        [InlineData("maxx")]
        [InlineData("miny")]
        [InlineData("maxy")]
        [InlineData("x")]
        [InlineData("y")]
        [InlineData("weight")]
        public async Task RasterTerrainHeatmap_NaNInAnyGuardedField_ThrowsArgumentException(string field)
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = Heatmap(4, 4, 2);
            switch (field)
            {
                case "minx": request.MinX = double.NaN; break;
                case "maxx": request.MaxX = double.NaN; break;
                case "miny": request.MinY = double.NaN; break;
                case "maxy": request.MaxY = double.NaN; break;
                case "x": request.Points[1].X = double.NaN; break;
                case "y": request.Points[1].Y = double.NaN; break;
                default: request.Points[1].Weight = double.NaN; break;
            }

            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHeatmapAsync(request));
        }
    }
}
