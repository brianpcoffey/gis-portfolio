using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Portfolio.Services.Abstractions;
using Portfolio.Services.Services;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Portfolio.Tests.Services
{
    public class BatchGeocodingServiceTests
    {
        private readonly Mock<ILogger<BatchGeocodingService>> _loggerMock = new();
        private readonly IDistributedCache _memoryCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        private readonly Mock<IBatchJobStore> _jobStoreMock = new();

        private BatchGeocodingService CreateService(string responseJson, double minScore = 80.0, int maxConcurrency = 5)
        {
            var inMemoryConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BatchGeocoding:MaxConcurrency"] = maxConcurrency.ToString(),
                    ["BatchGeocoding:MinMatchScore"] = minScore.ToString()
                })
                .Build();

            var handler = new FakeHttpMessageHandler(responseJson);
            var httpClient = new HttpClient(handler);

            return new BatchGeocodingService(httpClient, _memoryCache, _loggerMock.Object, _jobStoreMock.Object, inMemoryConfig);
        }

        private static IFormFile BuildCsvFile(string csvContent)
        {
            var bytes = Encoding.UTF8.GetBytes(csvContent);
            var stream = new MemoryStream(bytes);
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
            fileMock.Setup(f => f.Length).Returns(bytes.Length);
            return fileMock.Object;
        }

        private static HttpResponseMessage BuildGeocodeResponse(string address, double score, double x, double y)
        {
            var payload = new
            {
                candidates = new[]
                {
                    new { address, score, location = new { x, y } }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json")
            };
        }

        private static string BuildGeocodeJson(string address, double score, double x, double y)
        {
            var payload = new
            {
                candidates = new[]
                {
                    new { address, score, location = new { x, y } }
                }
            };
            return JsonSerializer.Serialize(payload);
        }

        [Fact]
        public async Task GeocodeAsync_AllRowsMatched_ReturnsMatchedStatus()
        {
            // Arrange
            var csv = "Id,Address,City,State,Zip\n1,123 Main St,Denver,CO,80201\n2,456 Oak Ave,Boulder,CO,80302";
            var file = BuildCsvFile(csv);
            var json = BuildGeocodeJson("123 Main St, Denver, CO 80201", 95.0, -104.9, 39.7);
            var service = CreateService(json);

            // Act
            var results = await service.GeocodeAsync(file);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.True(r.Matched));
        }

        [Fact]
        public async Task GeocodeAsync_SomeRowsBelowThreshold_MarkedUnmatched()
        {
            // Arrange
            var csv = "Id,Address,City,State,Zip\n1,123 Main St,Denver,CO,80201";
            var file = BuildCsvFile(csv);
            // Score below default threshold of 80
            var json = BuildGeocodeJson("123 Main St, Denver, CO 80201", 50.0, -104.9, 39.7);
            var service = CreateService(json, minScore: 80.0);

            // Act
            var results = await service.GeocodeAsync(file);

            // Assert
            Assert.Single(results);
            Assert.False(results[0].Matched);
            Assert.Equal(50.0, results[0].Score);
        }

        [Theory]
        [InlineData(null)]
        public async Task GeocodeAsync_NullFile_ThrowsArgumentException(IFormFile? file)
        {
            // Arrange
            var service = CreateService("{}");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => service.GeocodeAsync(file!, CancellationToken.None));
        }

        [Fact]
        public async Task GeocodeAsync_EmptyFile_ThrowsArgumentException()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(0);
            var service = CreateService("{}");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => service.GeocodeAsync(fileMock.Object, CancellationToken.None));
        }

        [Fact]
        public async Task GeocodeAsync_CsvWithHeaderOnly_ThrowsArgumentException()
        {
            // Arrange
            var csv = "Id,Address,City,State,Zip\n";
            var file = BuildCsvFile(csv);
            var service = CreateService("{}");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => service.GeocodeAsync(file, CancellationToken.None));
        }

        [Fact]
        public async Task GeocodeAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var csv = "Id,Address,City,State,Zip\n1,123 Main St,Denver,CO,80201";
            var file = BuildCsvFile(csv);

            using var cts = new CancellationTokenSource();

            // Use a handler that blocks until cancelled
            var blockingHandler = new BlockingHttpMessageHandler();
            var httpClient = new HttpClient(blockingHandler);

            var inMemoryConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BatchGeocoding:MaxConcurrency"] = "1",
                    ["BatchGeocoding:MinMatchScore"] = "80"
                })
                .Build();

            var service = new BatchGeocodingService(httpClient, _memoryCache, _loggerMock.Object, _jobStoreMock.Object, inMemoryConfig);

            cts.CancelAfter(50);

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.GeocodeAsync(file, cts.Token));
        }

        [Fact]
        public async Task GeocodeAsync_NoCandidatesReturned_MarkedUnmatched()
        {
            // Arrange
            var csv = "Id,Address,City,State,Zip\n1,Unknown Rd,Nowhere,XX,00000";
            var file = BuildCsvFile(csv);
            var emptyJson = JsonSerializer.Serialize(new { candidates = Array.Empty<object>() });
            var service = CreateService(emptyJson);

            // Act
            var results = await service.GeocodeAsync(file);

            // Assert
            Assert.Single(results);
            Assert.False(results[0].Matched);
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

        private sealed class BlockingHttpMessageHandler : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }
    }
}
