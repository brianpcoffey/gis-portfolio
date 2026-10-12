using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace Portfolio.Tests.Integration;

/// <summary>
/// Locks the retry budget of each ArcGIS typed client. The factory replaces the transport with a
/// fake that always answers 503 and counts requests, so the resilience pipeline from Program.cs is
/// exercised unchanged (retry + exponential backoff, about 1.5 s for features and 2.1 s for the
/// others) and nothing leaves the machine. Attempts = 1 + MaxRetryAttempts.
/// </summary>
public class ArcGisResilienceTests
{
    [Theory]
    [InlineData("IArcGisService", 3)]                  // arcgis-features: MaxRetryAttempts = 2
    [InlineData("IBatchGeocodingService", 4)]          // arcgis-batch:    MaxRetryAttempts = 3
    [InlineData("IReverseGeocodingService", 4)]        // arcgis-reverse:  MaxRetryAttempts = 3
    [InlineData("IAddressStandardizationService", 4)]  // arcgis-address:  MaxRetryAttempts = 3
    public async Task Retries503_UpToTheConfiguredBudget(string clientName, int expectedAttempts)
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        _ = f.Client(); // build the host
        var http = f.Services.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);

        // Act
        var r = await http.GetAsync("http://arcgis.invalid/probe", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        Assert.Equal(expectedAttempts, f.ArcGisAttempts(clientName));
    }
}
