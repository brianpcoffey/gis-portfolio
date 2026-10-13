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
            _controller = new SpatialOverlayController(
                _service.Object, NullLogger<SpatialOverlayController>.Instance);
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
        public async Task SpatialJoin_ServiceArgumentException_ReturnsBadRequestWithErrorBody()
        {
            // Arrange
            _service.Setup(s => s.SpatialJoinAsync(It.IsAny<SpatialJoinRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Bad zones."));

            // Act
            var result = await _controller.SpatialJoin(new SpatialJoinRequestDto(), CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result.Result, 400, "Bad zones.");
        }

        [Fact]
        public async Task SpatialJoin_RealServiceWithNoPoints_BodyCarriesParameterSuffix()
        {
            // Arrange
            var controller = new SpatialOverlayController(
                new SpatialOverlayService(NullLogger<SpatialOverlayService>.Instance),
                NullLogger<SpatialOverlayController>.Instance);

            // Act
            var result = await controller.SpatialJoin(new SpatialJoinRequestDto(), CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result.Result, 400, "At least one point is required. (Parameter 'request')");
        }
    }
}
