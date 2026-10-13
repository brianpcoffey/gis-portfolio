using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class SpatialOverlayControllerTests
    {
        private readonly Mock<ISpatialOverlayService> _service = new();
        private readonly SpatialOverlayController _controller;

        public SpatialOverlayControllerTests()
        {
            _controller = new SpatialOverlayController(_service.Object);
        }

        [Fact]
        public async Task SpatialJoin_ValidRequest_ReturnsOkWithResult()
        {
            // Arrange
            var expected = new SpatialJoinResultDto { AssignedCount = 3, UnassignedCount = 1 };
            _service.Setup(s => s.SpatialJoinAsync(It.IsAny<SpatialJoinRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var result = await _controller.SpatialJoin(new SpatialJoinRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(expected, ok.Value);
        }

        [Fact]
        public async Task SpatialJoin_ServiceArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            _service.Setup(s => s.SpatialJoinAsync(It.IsAny<SpatialJoinRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Bad zones."));

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _controller.SpatialJoin(new SpatialJoinRequestDto(), CancellationToken.None));
        }

        [Fact]
        public async Task SpatialJoin_RealServiceWithNoPoints_MessageCarriesParameterSuffix()
        {
            // Arrange
            var controller = new SpatialOverlayController(
                new SpatialOverlayService(NullLogger<SpatialOverlayService>.Instance));

            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => controller.SpatialJoin(new SpatialJoinRequestDto(), CancellationToken.None));

            // Assert: this is the text the filter puts in the 400 body.
            Assert.Equal("At least one point is required. (Parameter 'request')", ex.Message);
        }
    }
}
