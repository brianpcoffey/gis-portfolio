using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Portfolio.Services.Services;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Portfolio.Tests.Services
{
    public class ReverseGeocodingServiceTests
    {
        private readonly Mock<ILogger<ReverseGeocodingService>> _loggerMock = new();

        private ReverseGeocodingService CreateService(string responseJson, double gridResolution = 0.001, int slidingMinutes = 10)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ReverseGeocoding:GridResolutionDegrees"] = gridResolution.ToString(),
                    ["ReverseGeocoding:CacheSlidingExpirationMinutes"] = slidingMinutes.ToString()
                })
                .Build();

            // Use a fresh cache per test to avoid inter-test cache pollution.
            var cache = new MemoryCache(new MemoryCacheOptions());
            var handler = new FakeHttpMessageHandler(responseJson);
            var httpClient = new HttpClient(handler);

            return new ReverseGeocodingService(httpClient, cache, _loggerMock.Object, config);
        }

        private static string BuildArcGisResponse(
            string longLabel = "1600 Pennsylvania Ave NW, Washington, DC 20500",
            string addNum = "1600",
            string stAddr = "Pennsylvania Ave NW",
            string city = "Washington",
            string region = "District of Columbia",
            string postal = "20500",
            string countryCode = "USA",
            string addrType = "PointAddress")
        {
            var payload = new
            {
                address = new
                {
                    LongLabel = longLabel,
                    Match_addr = longLabel,
                    AddNum = addNum,
                    StAddr = stAddr,
                    City = city,
                    Region = region,
                    Postal = postal,
                    CountryCode = countryCode,
                    Addr_type = addrType
                }
            };
            return JsonSerializer.Serialize(payload);
        }

        [Fact]
        public async Task ReverseGeocodeAsync_ValidCoordinates_ReturnsPopulatedDto()
        {
            // Arrange
            var json = BuildArcGisResponse();
            var service = CreateService(json);

            // Act
            var result = await service.ReverseGeocodeAsync(38.8977, -77.0366);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("1600 Pennsylvania Ave NW, Washington, DC 20500", result.MatchedAddress);
            Assert.Equal("1600", result.HouseNumber);
            Assert.Equal("Pennsylvania Ave NW", result.Street);
            Assert.Equal("Washington", result.City);
            Assert.Equal("District of Columbia", result.Region);
            Assert.Equal("20500", result.PostalCode);
            Assert.Equal("USA", result.CountryCode);
            Assert.Equal("PointAddress", result.LocationType);
        }

        [Fact]
        public async Task ReverseGeocodeAsync_SameSnappedCoordinate_DoesNotCallServiceAgain()
        {
            // Arrange — two coordinates that snap to the same 0.001-degree grid cell
            var callCount = 0;
            var json = BuildArcGisResponse();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ReverseGeocoding:GridResolutionDegrees"] = "0.001",
                    ["ReverseGeocoding:CacheSlidingExpirationMinutes"] = "10"
                })
                .Build();

            var cache = new MemoryCache(new MemoryCacheOptions());
            var handler = new CountingFakeHandler(json, () => callCount++);
            var httpClient = new HttpClient(handler);
            var service = new ReverseGeocodingService(httpClient, cache, _loggerMock.Object, config);

            // Act — lat 38.8977 and 38.8975 both snap to 38.898 at 0.001 resolution
            await service.ReverseGeocodeAsync(38.8977, -77.0366);
            await service.ReverseGeocodeAsync(38.8975, -77.0366);

            // Assert — only one HTTP call was made
            Assert.Equal(1, callCount);
        }

        [Theory]
        [InlineData(-91.0, 0.0, "Latitude must be between -90 and 90")]
        [InlineData(90.1, 0.0, "Latitude must be between -90 and 90")]
        [InlineData(0.0, -181.0, "Longitude must be between -180 and 180")]
        [InlineData(0.0, 180.1, "Longitude must be between -180 and 180")]
        public async Task ReverseGeocodeAsync_OutOfRangeCoordinates_ThrowsArgumentException(double lat, double lng, string expectedMessage)
        {
            // Arrange
            var service = CreateService("{}");

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => service.ReverseGeocodeAsync(lat, lng));
            Assert.Contains(expectedMessage, ex.Message);
        }

        [Fact]
        public async Task ReverseGeocodeAsync_NoArcGisResult_ThrowsKeyNotFoundException()
        {
            // Arrange — ArcGIS returns no address object
            var json = JsonSerializer.Serialize(new { address = (object?)null });
            var service = CreateService(json);

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => service.ReverseGeocodeAsync(0.0, 0.0));
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
