using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Portfolio.Services.Services;
using System.Net;
using Xunit;

namespace Portfolio.Tests.Services
{
    public class ArcGisServiceTests
    {
        private readonly Mock<ILogger<ArcGisService>> _loggerMock;

        public ArcGisServiceTests()
        {
            _loggerMock = new Mock<ILogger<ArcGisService>>();
        }

        [Fact]
        public async Task QueryFeaturesAsync_WithNullLayerId_ThrowsArgumentException()
        {
            // Arrange
            var httpClient = new HttpClient();
            var service = new ArcGisService(httpClient, _loggerMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.QueryFeaturesAsync(null!));
        }

        [Fact]
        public async Task QueryFeaturesAsync_WhenCanceled_ThrowsOperationCanceledException()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var httpClient = new HttpClient();
            var service = new ArcGisService(httpClient, _loggerMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(
                () => service.QueryFeaturesAsync("1", null, cts.Token));
        }

        [Fact]
        public async Task QueryFeaturesAsync_WithValidResponse_ReturnsFeatures()
        {
            // Arrange
            var handlerMock = new Mock<HttpMessageHandler>();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                        "features": [
                            {
                                "attributes": { "OBJECTID": 1, "NAME": "Test Feature" },
                                "geometry": { "x": -100, "y": 40 }
                            }
                        ]
                    }
                    """)
            };

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var httpClient = new HttpClient(handlerMock.Object);
            var service = new ArcGisService(httpClient, _loggerMock.Object);

            // Act
            var result = await service.QueryFeaturesAsync("1");

            // Assert
            Assert.Single(result);
            Assert.Equal("Test Feature", result[0].Name);
        }
    }
}