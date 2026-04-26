using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
            _controller = new ReverseGeocodingController(
                _serviceMock.Object,
                new Mock<ILogger<ReverseGeocodingController>>().Object);
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
        public async Task GetPlaceData_OutOfRangeCoordinates_ReturnsBadRequest(double lat, double lng, string message, string paramName)
        {
            // Arrange
            _serviceMock
                .Setup(s => s.ReverseGeocodeAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException(message, paramName));

            // Act
            var result = await _controller.GetPlaceData(lat, lng, CancellationToken.None);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
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
    }
}
