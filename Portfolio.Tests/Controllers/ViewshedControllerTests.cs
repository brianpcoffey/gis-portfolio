using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class ViewshedControllerTests
    {
        private readonly Mock<IViewshedService> _service = new();
        private readonly ViewshedController _controller;

        public ViewshedControllerTests()
        {
            _controller = new ViewshedController(_service.Object);
        }

        [Fact]
        public async Task Compute_ValidRequest_ReturnsOkWithResult()
        {
            // Arrange
            var expected = new ViewshedResultDto { VisibleCells = 4, TotalCells = 4 };
            _service.Setup(s => s.ComputeAsync(It.IsAny<ViewshedRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var result = await _controller.Compute(new ViewshedRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(expected, ok.Value);
        }

        [Fact]
        public async Task Compute_ServiceArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            _service.Setup(s => s.ComputeAsync(It.IsAny<ViewshedRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Bad grid."));

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _controller.Compute(new ViewshedRequestDto(), CancellationToken.None));
        }

        [Fact]
        public async Task Compute_RealServiceWithEmptyGrid_MessageOmitsParameterSuffix()
        {
            // Arrange
            // ViewshedService.ValidateRasterShape throws without nameof(request), so unlike the
            // other spatial services the message has no " (Parameter 'request')" suffix.
            var controller = new ViewshedController(
                new ViewshedService(NullLogger<ViewshedService>.Instance));

            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => controller.Compute(
                new ViewshedRequestDto { Width = 0, Height = 0, Elevation = [] }, CancellationToken.None));

            // Assert: this is the text the filter puts in the 400 body.
            Assert.Equal("Raster dimensions must be greater than zero.", ex.Message);
        }

        [Fact]
        public async Task Compute_RealServiceWithoutElevation_MessageCarriesParameterSuffix()
        {
            // Arrange
            var controller = new ViewshedController(
                new ViewshedService(NullLogger<ViewshedService>.Instance));

            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => controller.Compute(
                new ViewshedRequestDto { Width = 2, Height = 2, Elevation = null! }, CancellationToken.None));

            // Assert: this is the text the filter puts in the 400 body.
            Assert.Equal("Elevation values are required. (Parameter 'request')", ex.Message);
        }
    }
}
