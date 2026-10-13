using Microsoft.AspNetCore.Mvc;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class ReverseGeocodingControllerTests
    {
        private readonly Mock<IReverseGeocodingService> _serviceMock;
        private readonly ReverseGeocodingController _controller;

        public ReverseGeocodingControllerTests()
        {
            _serviceMock = new Mock<IReverseGeocodingService>();
            _controller = new ReverseGeocodingController(_serviceMock.Object);
        }

        [Fact]
        public async Task GetPlaceData_ValidCoordinates_ReturnsOkWithDto()
        {
            // Arrange
            var dto = new ReverseGeocodingResultDto
            {
                Latitude = 38.8977,
                Longitude = -77.0366,
                MatchedAddress = "1600 Pennsylvania Ave NW, Washington, DC 20500",
                City = "Washington",
                Region = "District of Columbia",
                PostalCode = "20500",
                CountryCode = "USA",
                LocationType = "PointAddress"
            };

            _serviceMock
                .Setup(s => s.ReverseGeocodeAsync(38.8977, -77.0366, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            // Act
            var result = await _controller.GetPlaceData(38.8977, -77.0366, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(dto, ok.Value);
        }

        [Theory]
        [InlineData(91.0, 0.0, "Latitude must be between -90 and 90", "latitude")]
        [InlineData(-91.0, 0.0, "Latitude must be between -90 and 90", "latitude")]
        [InlineData(0.0, 181.0, "Longitude must be between -180 and 180", "longitude")]
        [InlineData(0.0, -181.0, "Longitude must be between -180 and 180", "longitude")]
        public async Task GetPlaceData_OutOfRangeCoordinates_Propagates(double lat, double lng, string message, string paramName)
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            _serviceMock
                .Setup(s => s.ReverseGeocodeAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException(message, paramName));

            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _controller.GetPlaceData(lat, lng, CancellationToken.None));

            // Assert: the exception propagates from the action unchanged. The filter shows its message only
            // when Portfolio code threw it; an exception handed back by a mock is rethrown by .NET, so it gets the generic text.
            Assert.Equal($"{message} (Parameter '{paramName}')", ex.Message);
        }

        [Fact]
        public async Task GetPlaceData_NoArcGisResult_ReturnsNotFound()
        {
            // Arrange
            _serviceMock
                .Setup(s => s.ReverseGeocodeAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("No result found."));

            // Act
            var result = await _controller.GetPlaceData(0.0, 0.0, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GetPlaceData_BrokenCircuit_Propagates()
        {
            // Arrange: [ArcGisUnavailable] turns it into the 503 body (see the filter tests).
            _serviceMock
                .Setup(s => s.ReverseGeocodeAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Polly.CircuitBreaker.BrokenCircuitException("circuit open"));

            // Act & Assert
            await Assert.ThrowsAsync<Polly.CircuitBreaker.BrokenCircuitException>(
                () => _controller.GetPlaceData(39.7, -104.9, CancellationToken.None));
        }

        [Fact]
        public async Task GetPlaceData_KeyNotFound_ReturnsBareNotFound()
        {
            // Arrange
            _serviceMock.Setup(s => s.ReverseGeocodeAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("no match"));

            // Act
            var result = await _controller.GetPlaceData(1, 1, CancellationToken.None);

            // Assert
            var notFound = Assert.IsType<NotFoundResult>(result);
            Assert.Equal(404, notFound.StatusCode);
        }
    }
}
