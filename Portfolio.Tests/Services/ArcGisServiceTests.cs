using Microsoft.Extensions.Logging;
using Moq;
using Portfolio.Services.Services;
using System.Net;
using System.Text.Json;

namespace Portfolio.Tests.Services
{
    public class ArcGisServiceTests
    {
        private readonly Mock<ILogger<ArcGisService>> _loggerMock = new();

        private ArcGisService CreateService(HttpResponseMessage response)
        {
            var handler = new FakeHttpMessageHandler(response);
            var httpClient = new HttpClient(handler);
            return new ArcGisService(httpClient, _loggerMock.Object);
        }

        [Fact]
        public async Task QueryFeaturesAsync_WithValidResponse_ReturnsMappedFeatures()
        {
            var payload = new
            {
                features = new[]
                {
                    new
                    {
                        attributes = new Dictionary<string, object?>
                        {
                            ["OBJECTID"] = 1,
                            ["STATE_NAME"] = "Colorado"
                        },
                        geometry = new { x = -105.7, y = 39.0 }
                    }
                }
            };
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
            };

            var service = CreateService(response);
            var result = await service.QueryFeaturesAsync("3");

            Assert.Single(result);
            Assert.Equal("Colorado", result[0].Name);
            Assert.Equal("3", result[0].LayerId);
        }

        [Fact]
        public async Task QueryFeaturesAsync_WithEmptyResponse_ReturnsEmptyList()
        {
            var payload = new { features = Array.Empty<object>() };
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
            };

            var service = CreateService(response);
            var result = await service.QueryFeaturesAsync("3");

            Assert.Empty(result);
        }

        [Fact]
        public async Task QueryFeaturesAsync_WithHttpError_ReturnsEmptyList()
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            var service = CreateService(response);

            var result = await service.QueryFeaturesAsync("3");

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFeatureAsync_WhenFeatureExists_ReturnsFeature()
        {
            var payload = new
            {
                features = new[]
                {
                    new
                    {
                        attributes = new Dictionary<string, object?> { ["OBJECTID"] = 42, ["STATE_NAME"] = "Texas" },
                        geometry = new { x = -97.0, y = 31.0 }
                    },
                    new
                    {
                        attributes = new Dictionary<string, object?> { ["OBJECTID"] = 43, ["STATE_NAME"] = "Utah" },
                        geometry = new { x = -111.0, y = 39.3 }
                    }
                }
            };
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
            };

            var service = CreateService(response);
            var result = await service.GetFeatureAsync("3", "42");

            Assert.NotNull(result);
            Assert.Equal("Texas", result!.Name);
        }

        [Fact]
        public async Task GetFeatureAsync_WhenFeatureNotFound_ReturnsNull()
        {
            var payload = new
            {
                features = new[]
                {
                    new
                    {
                        attributes = new Dictionary<string, object?> { ["OBJECTID"] = 1, ["STATE_NAME"] = "Maine" },
                        geometry = new { x = -69.4, y = 45.2 }
                    }
                }
            };
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
            };

            var service = CreateService(response);
            var result = await service.GetFeatureAsync("3", "999");

            Assert.Null(result);
        }

        /// <summary>
        /// Fake handler to supply canned HTTP responses for testing.
        /// </summary>
        private sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(response);
        }
    }
}