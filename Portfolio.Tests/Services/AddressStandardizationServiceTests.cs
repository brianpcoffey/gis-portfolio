using Microsoft.Extensions.Logging;
using Moq;
using Portfolio.Common.Enums;
using Portfolio.Services.Services;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Portfolio.Tests.Services
{
    public class AddressStandardizationServiceTests
    {
        private readonly Mock<ILogger<AddressStandardizationService>> _loggerMock = new();

        private AddressStandardizationService CreateService(string responseJson)
        {
            var handler = new FakeHttpMessageHandler(responseJson);
            var httpClient = new HttpClient(handler);
            return new AddressStandardizationService(httpClient, _loggerMock.Object);
        }

        private static string BuildCandidateResponse(string address, double score)
        {
            var payload = new
            {
                candidates = new[]
                {
                    new { address, score }
                }
            };
            return JsonSerializer.Serialize(payload);
        }

        private static string BuildEmptyCandidateResponse()
        {
            return JsonSerializer.Serialize(new { candidates = Array.Empty<object>() });
        }

        // ── Parse happy path ──────────────────────────────────────────────────

        [Fact]
        public async Task ParseAsync_WellFormedAddress_ExtractsAllComponents()
        {
            // Arrange
            var service = CreateService("{}");

            // Act
            var result = await service.ParseAsync("123 Main St, Springfield, IL 62701");

            // Assert
            Assert.Equal("123", result.HouseNumber);
            Assert.Equal("Main", result.StreetName);
            Assert.Equal("Street", result.StreetSuffix);
            Assert.Equal("Springfield", result.City);
            Assert.Equal("IL", result.State);
            Assert.Equal("62701", result.PostalCode);
            Assert.Equal(1.0, result.ParseConfidence);
            Assert.Contains("123 Main Street", result.StandardizedAddress);
            Assert.Contains("Springfield", result.StandardizedAddress);
            Assert.Contains("IL", result.StandardizedAddress);
            Assert.Contains("62701", result.StandardizedAddress);
        }

        [Fact]
        public async Task ParseAsync_AddressWithUnit_ExtractsUnitComponent()
        {
            // Arrange
            var service = CreateService("{}");

            // Act
            var result = await service.ParseAsync("456 Oak Ave Apt 4B, Los Angeles, CA 90001");

            // Assert
            Assert.Equal("456", result.HouseNumber);
            Assert.Equal("Avenue", result.StreetSuffix);
            Assert.Contains("4B", result.Unit);
            Assert.Equal("Los Angeles", result.City);
            Assert.Equal("CA", result.State);
            Assert.Equal("90001", result.PostalCode);
            Assert.Contains("Apt 4B", result.StandardizedAddress);
        }

        // ── Parse partial: missing ZIP reduces confidence ─────────────────────

        [Fact]
        public async Task ParseAsync_MissingZip_ReturnsReducedConfidence()
        {
            // Arrange
            var service = CreateService("{}");

            // Act
            var result = await service.ParseAsync("123 Main St, Denver, CO");

            // Assert
            Assert.Equal("123", result.HouseNumber);
            Assert.Equal("CO", result.State);
            Assert.Empty(result.PostalCode);
            Assert.True(result.ParseConfidence < 1.0);
            Assert.Equal(0.8, result.ParseConfidence, precision: 5); // 4 of 5 components
        }

        // ── Parse empty input ─────────────────────────────────────────────────

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ParseAsync_EmptyInput_ThrowsArgumentException(string? rawAddress)
        {
            // Arrange
            var service = CreateService("{}");

            // Act & Assert
            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => service.ParseAsync(rawAddress!, CancellationToken.None));
        }

        // ── Validate happy path: high-score returns High tier ─────────────────

        [Fact]
        public async Task ValidateAsync_HighScoreResult_ReturnsHighTier()
        {
            // Arrange
            var json = BuildCandidateResponse("123 Main Street, Springfield, IL 62701, USA", 95.0);
            var service = CreateService(json);

            // Act
            var result = await service.ValidateAsync("123 Main St, Springfield, IL 62701");

            // Assert
            Assert.Equal(ConfidenceTier.High, result.ConfidenceTier);
            Assert.Equal(95.0, result.Score);
            Assert.NotEmpty(result.MatchedAddress);
        }

        // ── Validate Medium tier ──────────────────────────────────────────────

        [Fact]
        public async Task ValidateAsync_MediumScoreResult_ReturnsMediumTier()
        {
            // Arrange
            var json = BuildCandidateResponse("123 Main Street, Springfield, IL", 80.0);
            var service = CreateService(json);

            // Act
            var result = await service.ValidateAsync("123 Main St, Springfield, IL 62701");

            // Assert
            Assert.Equal(ConfidenceTier.Medium, result.ConfidenceTier);
        }

        // ── Validate fallback: first pass low, fallback succeeds → Low tier ───

        [Fact]
        public async Task ValidateAsync_LowFirstPassScore_TriggersAndReturnsFallbackAsLow()
        {
            // Arrange — first call returns 60 (below 75); fallback call returns 60 too (>= 50 → Low)
            var callCount = 0;
            var lowJson  = BuildCandidateResponse("Springfield, IL 62701, USA", 60.0);
            var config = new System.Collections.Generic.Dictionary<string, string?>();

            var handler = new CountingFakeHandler(lowJson, () => callCount++);
            var httpClient = new HttpClient(handler);
            var service = new AddressStandardizationService(httpClient, _loggerMock.Object);

            // Act
            var result = await service.ValidateAsync("123 Main St, Springfield, IL 62701");

            // Assert — two HTTP calls were made (primary + fallback)
            Assert.Equal(2, callCount);
            Assert.Equal(ConfidenceTier.Low, result.ConfidenceTier);
        }

        // ── Validate unresolved: no candidates → Unresolved tier ─────────────

        [Fact]
        public async Task ValidateAsync_NoCandidates_ReturnsUnresolvedTier()
        {
            // Arrange
            var json = BuildEmptyCandidateResponse();
            var service = CreateService(json);

            // Act
            var result = await service.ValidateAsync("999 Nowhere Rd, Faketown, XX 00000");

            // Assert
            Assert.Equal(ConfidenceTier.Unresolved, result.ConfidenceTier);
            Assert.Equal(0.0, result.Score);
        }

        // ── Suffix normalisation [Theory] covering all 9 abbreviations ────────

        [Theory]
        [InlineData("100 Oak St, Denver, CO 80201",   "Street")]
        [InlineData("100 Oak Ave, Denver, CO 80201",  "Avenue")]
        [InlineData("100 Oak Blvd, Denver, CO 80201", "Boulevard")]
        [InlineData("100 Oak Dr, Denver, CO 80201",   "Drive")]
        [InlineData("100 Oak Rd, Denver, CO 80201",   "Road")]
        [InlineData("100 Oak Ln, Denver, CO 80201",   "Lane")]
        [InlineData("100 Oak Ct, Denver, CO 80201",   "Court")]
        [InlineData("100 Oak Pl, Denver, CO 80201",   "Place")]
        [InlineData("100 Oak Hwy, Denver, CO 80201",  "Highway")]
        public async Task ParseAsync_SuffixAbbreviation_ExpandsToFullWord(string input, string expectedSuffix)
        {
            // Arrange
            var service = CreateService("{}");

            // Act
            var result = await service.ParseAsync(input);

            // Assert
            Assert.Equal(expectedSuffix, result.StreetSuffix);
        }

        private sealed class FakeHttpMessageHandler(string json) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        }

        private sealed class CountingFakeHandler(string json, Action onCall) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                onCall();
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        }
    }
}
