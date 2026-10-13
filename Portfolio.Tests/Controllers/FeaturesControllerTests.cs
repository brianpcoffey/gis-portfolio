using Microsoft.AspNetCore.Mvc;
using Moq;
using Polly.CircuitBreaker;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class FeaturesControllerTests
    {
        private readonly Mock<IArcGisService> _serviceMock;
        private readonly FeaturesController _controller;

        public FeaturesControllerTests()
        {
            _serviceMock = new Mock<IArcGisService>();
            _controller = new FeaturesController(_serviceMock.Object);
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
        public async Task GetFeatures_WhenCircuitBreakerIsOpen_Propagates()
        {
            // Arrange: [ArcGisUnavailable(Features)] turns it into the 503 body (see the filter tests).
            _serviceMock
                .Setup(s => s.QueryFeaturesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new BrokenCircuitException("circuit open"));

            // Act & Assert
            await Assert.ThrowsAsync<BrokenCircuitException>(
                () => _controller.GetFeatures("1", null, CancellationToken.None));
        }

        [Fact]
        public async Task GetFeatures_BlankLayerId_PinsExactErrorBody()
        {
            // Act
            var result = await _controller.GetFeatures(" ", null, CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400, "layerId is required.");
        }

        [Theory]
        [InlineData("..")]
        [InlineData("1/../2")]
        [InlineData("abc")]
        [InlineData("1234")]
        [InlineData("-1")]
        [InlineData("1.5")]
        [InlineData("1\n")]
        [InlineData("\u0663")] // Arabic-Indic digit three
        public async Task GetFeatures_NonNumericOrLongLayerId_Returns400_AndNeverCallsTheService(string layerId)
        {
            // Act
            var result = await _controller.GetFeatures(layerId, null, CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400, "layerId must be 1 to 3 digits.");
            _serviceMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("0")]
        [InlineData("12")]
        [InlineData("999")]
        public async Task GetFeatures_OneToThreeDigitLayerId_IsAccepted(string layerId)
        {
            // Arrange
            _serviceMock.Setup(s => s.QueryFeaturesAsync(layerId, null, It.IsAny<CancellationToken>())).ReturnsAsync([]);

            // Act
            var result = await _controller.GetFeatures(layerId, null, CancellationToken.None);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        [Theory]
        [InlineData("1,2,3")]
        [InlineData("1,2,3,4,5")]
        [InlineData("a,b,c,d")]
        [InlineData("1,2,3,NaN")]
        [InlineData("1,2,3,Infinity")]
        [InlineData("1,2,3,")]
        [InlineData("1,2,3,1e999")]
        [InlineData(" ")]
        [InlineData("1,2,3,4&x=1")]
        public async Task GetFeatures_MalformedBbox_Returns400_AndNeverCallsTheService(string bbox)
        {
            // Act
            var result = await _controller.GetFeatures("1", bbox, CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400, "bbox must be four comma-separated numbers: minX,minY,maxX,maxY.");
            _serviceMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("-90.5,30,-80,40.25")]
        [InlineData("-1e2,0,1E2,5")]
        [InlineData("")]
        public async Task GetFeatures_WellFormedOrEmptyBbox_IsAccepted(string bbox)
        {
            // Arrange
            _serviceMock.Setup(s => s.QueryFeaturesAsync("1", bbox, It.IsAny<CancellationToken>())).ReturnsAsync([]);

            // Act
            var result = await _controller.GetFeatures("1", bbox, CancellationToken.None);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }
    }
}
