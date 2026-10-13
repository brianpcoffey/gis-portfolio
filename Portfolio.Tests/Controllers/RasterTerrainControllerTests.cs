using Microsoft.AspNetCore.Mvc;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class RasterTerrainControllerTests
    {
        [Fact]
        public async Task RasterTerrainHillshade_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IRasterTerrainService>();
            service.Setup(s => s.GenerateHillshadeAsync(It.IsAny<RasterHillshadeRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RasterHillshadeResultDto { Width = 1, Height = 1 });
            var controller = new RasterTerrainController(service.Object);

            // Act
            var result = await controller.Hillshade(new RasterHillshadeRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<RasterHillshadeResultDto>(ok.Value);
        }

        [Fact]
        public async Task RasterTerrainHeatmap_ServiceArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            var service = new Mock<IRasterTerrainService>();
            service.Setup(s => s.GenerateHeatmapAsync(It.IsAny<HeatmapRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid heatmap."));
            var controller = new RasterTerrainController(service.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => controller.Heatmap(new HeatmapRequestDto(), CancellationToken.None));
        }

        [Fact]
        public async Task RasterTerrainHillshade_ServiceArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            var service = new Mock<IRasterTerrainService>();
            service.Setup(s => s.GenerateHillshadeAsync(It.IsAny<RasterHillshadeRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid raster."));
            var controller = new RasterTerrainController(service.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => controller.Hillshade(new RasterHillshadeRequestDto(), CancellationToken.None));
        }

        [Fact]
        public async Task RasterTerrainHeatmap_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IRasterTerrainService>();
            service.Setup(s => s.GenerateHeatmapAsync(It.IsAny<HeatmapRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HeatmapResultDto { Width = 2, Height = 2 });
            var controller = new RasterTerrainController(service.Object);

            // Act
            var result = await controller.Heatmap(new HeatmapRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<HeatmapResultDto>(ok.Value);
        }
    }
}
