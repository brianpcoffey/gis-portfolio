using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Common.Enums;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class AddressStandardizationControllerTests
    {
        private readonly Mock<IAddressStandardizationService> _serviceMock;
        private readonly AddressStandardizationController _controller;

        public AddressStandardizationControllerTests()
        {
            _serviceMock = new Mock<IAddressStandardizationService>();
            _controller = new AddressStandardizationController(
                _serviceMock.Object,
                new Mock<ILogger<AddressStandardizationController>>().Object);
        }

        // ── /parse ────────────────────────────────────────────────────────────

        [Fact]
        public async Task Parse_ValidRequest_ReturnsOkWithParsedDto()
        {
            // Arrange
            var dto = new AddressParsedDto
            {
                HouseNumber = "123",
                StreetName  = "Main",
                StreetSuffix = "Street",
                City        = "Springfield",
                State       = "IL",
                PostalCode  = "62701",
                StandardizedAddress = "123 Main Street, Springfield, IL 62701",
                ParseConfidence = 1.0
            };

            _serviceMock
                .Setup(s => s.ParseAsync("123 Main St, Springfield, IL 62701", It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            var request = new AddressParseRequestDto { RawAddress = "123 Main St, Springfield, IL 62701" };

            // Act
            var result = await _controller.Parse(request, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(dto, ok.Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Parse_EmptyRawAddress_ReturnsBadRequest(string? rawAddress)
        {
            // Arrange
            var request = new AddressParseRequestDto { RawAddress = rawAddress! };

            // Act
            var result = await _controller.Parse(request, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Parse_ServiceThrowsArgumentException_ReturnsBadRequest()
        {
            // Arrange
            _serviceMock
                .Setup(s => s.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("RawAddress is required."));

            var request = new AddressParseRequestDto { RawAddress = " " };

            // Act
            var result = await _controller.Parse(request, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        // ── /validate ─────────────────────────────────────────────────────────

        [Fact]
        public async Task Validate_ValidRequest_ReturnsOkWithValidationDto()
        {
            // Arrange
            var dto = new AddressValidationResultDto
            {
                Parsed = new AddressParsedDto { StandardizedAddress = "123 Main Street, Springfield, IL 62701" },
                MatchedAddress = "123 Main Street, Springfield, IL 62701, USA",
                Score = 95.0,
                ConfidenceTier = ConfidenceTier.High
            };

            _serviceMock
                .Setup(s => s.ValidateAsync("123 Main St, Springfield, IL 62701", It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            var request = new AddressParseRequestDto { RawAddress = "123 Main St, Springfield, IL 62701" };

            // Act
            var result = await _controller.Validate(request, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(dto, ok.Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Validate_EmptyRawAddress_ReturnsBadRequest(string? rawAddress)
        {
            // Arrange
            var request = new AddressParseRequestDto { RawAddress = rawAddress! };

            // Act
            var result = await _controller.Validate(request, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Validate_ServiceThrowsArgumentException_ReturnsBadRequest()
        {
            // Arrange
            _serviceMock
                .Setup(s => s.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("RawAddress is required."));

            var request = new AddressParseRequestDto { RawAddress = "bad" };

            // Act
            var result = await _controller.Validate(request, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        // ── Circuit-breaker resilience ──────────────────────────────────────────

        [Fact]
        public async Task Parse_BrokenCircuit_Returns503WithProblemDetails()
        {
            // Arrange
            _serviceMock
                .Setup(s => s.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Polly.CircuitBreaker.BrokenCircuitException("circuit open"));

            var request = new AddressParseRequestDto { RawAddress = "123 Main St" };

            // Act
            var result = await _controller.Parse(request, CancellationToken.None);

            // Assert
            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(status.Value);
            Assert.Equal(503, problem.Status);
            Assert.Contains("15", problem.Extensions["retryAfterSeconds"]?.ToString());
        }

        [Fact]
        public async Task Validate_BrokenCircuit_Returns503WithProblemDetails()
        {
            // Arrange
            _serviceMock
                .Setup(s => s.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Polly.CircuitBreaker.BrokenCircuitException("circuit open"));

            var request = new AddressParseRequestDto { RawAddress = "123 Main St" };

            // Act
            var result = await _controller.Validate(request, CancellationToken.None);

            // Assert
            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(status.Value);
            Assert.Equal(503, problem.Status);
            Assert.Contains("15", problem.Extensions["retryAfterSeconds"]?.ToString());
        }
    }
}
