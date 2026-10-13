using Microsoft.AspNetCore.Mvc;
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
            _controller = new AddressStandardizationController(_serviceMock.Object);
        }

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
        public async Task Parse_ServiceThrowsArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            _serviceMock
                .Setup(s => s.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("RawAddress is required."));

            var request = new AddressParseRequestDto { RawAddress = "bad" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _controller.Parse(request, CancellationToken.None));
        }

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
        public async Task Validate_ServiceThrowsArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            _serviceMock
                .Setup(s => s.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("RawAddress is required."));

            var request = new AddressParseRequestDto { RawAddress = "bad" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _controller.Validate(request, CancellationToken.None));
        }

        [Fact]
        public async Task Parse_BrokenCircuit_Propagates()
        {
            // Arrange: [ArcGisUnavailable] turns it into the 503 body (see the filter tests).
            _serviceMock
                .Setup(s => s.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Polly.CircuitBreaker.BrokenCircuitException("circuit open"));

            var request = new AddressParseRequestDto { RawAddress = "123 Main St" };

            // Act & Assert
            await Assert.ThrowsAsync<Polly.CircuitBreaker.BrokenCircuitException>(
                () => _controller.Parse(request, CancellationToken.None));
        }

        [Fact]
        public async Task Validate_BrokenCircuit_Propagates()
        {
            // Arrange: [ArcGisUnavailable] turns it into the 503 body (see the filter tests).
            _serviceMock
                .Setup(s => s.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Polly.CircuitBreaker.BrokenCircuitException("circuit open"));

            var request = new AddressParseRequestDto { RawAddress = "123 Main St" };

            // Act & Assert
            await Assert.ThrowsAsync<Polly.CircuitBreaker.BrokenCircuitException>(
                () => _controller.Validate(request, CancellationToken.None));
        }

        private static AddressStandardizationController NewPinned() =>
            new(new Mock<IAddressStandardizationService>().Object);

        [Fact]
        public async Task Parse_BlankAddress_PinsExactErrorBody()
        {
            // Arrange
            var controller = NewPinned();

            // Act
            var result = await controller.Parse(new AddressParseRequestDto { RawAddress = " " }, CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400, "RawAddress is required.");
        }

        [Fact]
        public async Task Validate_BlankAddress_PinsExactErrorBody()
        {
            // Arrange
            var controller = NewPinned();

            // Act
            var result = await controller.Validate(new AddressParseRequestDto { RawAddress = "" }, CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400, "RawAddress is required.");
        }
    }
}
