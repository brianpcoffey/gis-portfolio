using Microsoft.AspNetCore.Mvc;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class GeoStreamControllerTests
    {
        [Fact]
        public async Task GeoStreamProcessEvents_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IGeoStreamProcessorService>();
            service.Setup(s => s.ProcessBatchAsync(It.IsAny<GeoStreamBatchRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GeoStreamBatchResultDto { TotalEvents = 1 });
            var controller = new GeoStreamController(service.Object);

            // Act
            var result = await controller.ProcessEvents(new GeoStreamBatchRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<GeoStreamBatchResultDto>(ok.Value);
        }

        [Fact]
        public async Task GeoStreamProcessEvents_ServiceArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            var service = new Mock<IGeoStreamProcessorService>();
            service.Setup(s => s.ProcessBatchAsync(It.IsAny<GeoStreamBatchRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid telemetry."));
            var controller = new GeoStreamController(service.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => controller.ProcessEvents(new GeoStreamBatchRequestDto(), CancellationToken.None));
        }

        [Fact]
        public async Task GeoStreamProcessEvents_ReturnsNativeAcceleratedFlag()
        {
            // Arrange — service reports native acceleration used
            var service = new Mock<IGeoStreamProcessorService>();
            service.Setup(s => s.ProcessBatchAsync(It.IsAny<GeoStreamBatchRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GeoStreamBatchResultDto { TotalEvents = 5, NativeAccelerated = true });
            var controller = new GeoStreamController(service.Object);

            // Act
            var result = await controller.ProcessEvents(new GeoStreamBatchRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<GeoStreamBatchResultDto>(ok.Value);
            Assert.True(dto.NativeAccelerated);
        }
    }
}
