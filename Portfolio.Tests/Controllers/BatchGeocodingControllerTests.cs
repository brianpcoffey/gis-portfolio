using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class BatchGeocodingControllerTests
    {
        private readonly Mock<IBatchGeocodingService> _serviceMock;
        private readonly BatchGeocodingController _controller;

        public BatchGeocodingControllerTests()
        {
            _serviceMock = new Mock<IBatchGeocodingService>();
            _controller = new BatchGeocodingController(
                _serviceMock.Object,
                new Mock<ILogger<BatchGeocodingController>>().Object);
        }

        private static IFormFile BuildFile(long length)
        {
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(length);
            fileMock.Setup(f => f.FileName).Returns("addresses.csv");
            return fileMock.Object;
        }

        [Fact]
        public async Task GeocodeAddresses_ValidFile_ReturnsOkWithResults()
        {
            // Arrange
            var file = BuildFile(128);
            var results = new List<BatchGeocodingResultDto>
            {
                new()
                {
                    OriginalAddress = "123 Main St, Denver, CO 80201",
                    Matched = true,
                    MatchedAddress = "123 Main St, Denver, CO 80201",
                    Score = 95.0,
                    Latitude = 39.7,
                    Longitude = -104.9
                }
            };

            _serviceMock
                .Setup(s => s.GeocodeAsync(file, It.IsAny<CancellationToken>()))
                .ReturnsAsync(results);

            // Act
            var result = await _controller.GeocodeAddresses(file, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Same(results, ok.Value);
        }

        [Fact]
        public async Task GeocodeAddresses_NullFile_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.GeocodeAddresses(null!, CancellationToken.None);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }

        [Fact]
        public async Task GeocodeAddresses_EmptyFile_ReturnsBadRequest()
        {
            // Arrange
            var file = BuildFile(0);

            // Act
            var result = await _controller.GeocodeAddresses(file, CancellationToken.None);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }

        [Fact]
        public async Task GeocodeAddresses_ServiceThrowsArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var file = BuildFile(64);

            _serviceMock
                .Setup(s => s.GeocodeAsync(file, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("The CSV file contains no data rows.", "file"));

            // Act
            var result = await _controller.GeocodeAddresses(file, CancellationToken.None);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }
    }
}
