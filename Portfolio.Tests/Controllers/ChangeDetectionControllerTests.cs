using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class ChangeDetectionControllerTests
    {
        [Fact]
        public async Task ChangeScene_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IChangeDetectionService>();
            service.Setup(s => s.GetSceneAsync(256, 256, 0.025, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChangeSceneDto { Width = 256, Height = 256, BandCount = 4 });
            var controller = new ChangeDetectionController(service.Object, NullLogger<ChangeDetectionController>.Instance);

            // Act
            var result = await controller.Scene(256, 256, 0.025, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var scene = Assert.IsType<ChangeSceneDto>(ok.Value);
            Assert.Equal(4, scene.BandCount);
        }

        [Fact]
        public async Task ChangeScene_ServiceArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var service = new Mock<IChangeDetectionService>();
            service.Setup(s => s.GetSceneAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Raster dimensions are limited to 512."));
            var controller = new ChangeDetectionController(service.Object, NullLogger<ChangeDetectionController>.Instance);

            // Act
            var result = await controller.Scene(4096, 4096, 0.025, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task ChangeDetect_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IChangeDetectionService>();
            service.Setup(s => s.DetectAsync(It.IsAny<DetectRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DetectResultDto { Width = 8, Height = 8, ThresholdMode = "otsu" });
            var controller = new ChangeDetectionController(service.Object, NullLogger<ChangeDetectionController>.Instance);

            // Act
            var result = await controller.Detect(new DetectRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var detection = Assert.IsType<DetectResultDto>(ok.Value);
            Assert.Equal("otsu", detection.ThresholdMode);
        }

        [Fact]
        public async Task ChangeDetect_ServiceArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var service = new Mock<IChangeDetectionService>();
            service.Setup(s => s.DetectAsync(It.IsAny<DetectRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Epoch raster length must equal width * height * bandCount."));
            var controller = new ChangeDetectionController(service.Object, NullLogger<ChangeDetectionController>.Instance);

            // Act
            var result = await controller.Detect(new DetectRequestDto(), CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task ChangeDetect_NullRequest_ReturnsBadRequest()
        {
            // Arrange — ArgumentNullException derives from ArgumentException, so ThrowIfNull
            // is covered by the same catch clause.
            var service = new Mock<IChangeDetectionService>();
            service.Setup(s => s.DetectAsync(It.IsAny<DetectRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentNullException("request"));
            var controller = new ChangeDetectionController(service.Object, NullLogger<ChangeDetectionController>.Instance);

            // Act
            var result = await controller.Detect(null!, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
