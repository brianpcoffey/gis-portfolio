using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Polly.CircuitBreaker;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;
using Xunit;

namespace Portfolio.Tests.Controllers
{
    public class FeaturesControllerTests
    {
        private readonly Mock<IArcGisService> _serviceMock;
        private readonly FeaturesController _controller;

        public FeaturesControllerTests()
        {
            _serviceMock = new Mock<IArcGisService>();
            _controller = new FeaturesController(
                _serviceMock.Object,
                new Mock<ILogger<FeaturesController>>().Object);
        }

        [Fact]
        public async Task GetFeatures_WithValidLayerId_ReturnsOk()
        {
            // Arrange
            var features = new List<FeatureDto> { new() { LayerId = "1", FeatureId = "100", Name = "Test" } };
            _serviceMock.Setup(s => s.QueryFeaturesAsync("1", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(features);

            // Act
            var result = await _controller.GetFeatures("1", null, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(features, ok.Value);
        }

        [Fact]
        public async Task GetFeatures_WithBbox_PassesBboxToService()
        {
            // Arrange
            _serviceMock.Setup(s => s.QueryFeaturesAsync("1", "-90,30,-80,40", It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            // Act
            var result = await _controller.GetFeatures("1", "-90,30,-80,40", CancellationToken.None);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            _serviceMock.Verify(s => s.QueryFeaturesAsync("1", "-90,30,-80,40", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetFeatures_WithMissingLayerId_ReturnsBadRequest(string? layerId)
        {
            // Act
            var result = await _controller.GetFeatures(layerId!, null, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetFeatures_WhenCircuitBreakerIsOpen_Returns503WithProblemDetails()
        {
            // Arrange
            _serviceMock
                .Setup(s => s.QueryFeaturesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new BrokenCircuitException("circuit open"));

            // Act
            var result = await _controller.GetFeatures("1", null, CancellationToken.None);

            // Assert
            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(status.Value);
            Assert.Equal(503, problem.Status);
            Assert.Equal("Feature Service Unavailable", problem.Title);
        }

        [Fact]
        public async Task GetFeatures_BlankLayerId_PinsExactErrorBody()
        {
            // Act
            var result = await _controller.GetFeatures(" ", null, CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400, "layerId is required.");
        }

        [Fact]
        public async Task GetFeatures_BrokenCircuit_PinsExact503Body()
        {
            // Arrange
            _serviceMock
                .Setup(s => s.QueryFeaturesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new BrokenCircuitException("open"));

            // Act
            var result = await _controller.GetFeatures("1", null, CancellationToken.None);

            // Assert
            ControllerBodyAssert.Unavailable(result, "Feature Service Unavailable",
                "The upstream GIS feature service is temporarily unavailable. Retry after 30 seconds.", 30);
        }
    }
}
