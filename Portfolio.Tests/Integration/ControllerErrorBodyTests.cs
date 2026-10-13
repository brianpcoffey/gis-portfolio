using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Polly.CircuitBreaker;
using Portfolio.Common.DTOs;
using Portfolio.Services.Abstractions;
using Portfolio.Services.Interfaces;

namespace Portfolio.Tests.Integration;

/// <summary>
/// WP-43 / D-17: pins the exact wire bodies of the two controller error shapes that the
/// <c>[BadRequestOnArgumentException]</c> and <c>[ArcGisUnavailable]</c> filters now produce, through
/// the real host. They pass on the old try/catch code and on the filters, so they prove the move did
/// not change a byte of the response. The 400 cases use the real services (no stubs) so the message
/// text is the one a client sees; the 503 cases stub the service to throw the breaker exception
/// (opening a real breaker takes several slow retried requests). Nothing leaves the machine: the
/// factory replaces every ArcGIS transport. One factory per test, because rate-limit state lives in
/// the host.
/// </summary>
public class ControllerErrorBodyTests
{
    private const string GeocodingDetail =
        "The upstream geocoding service is temporarily unavailable. Retry after 15 seconds.";

    private static HttpClient ClientWith(PortfolioAppFactory f, Action<IServiceCollection> configure) =>
        f.WithWebHostBuilder(b => b.ConfigureTestServices(configure))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task AssertBody(HttpResponseMessage r, HttpStatusCode status, string mediaType, string body)
    {
        Assert.Equal(status, r.StatusCode);
        Assert.Equal(mediaType, r.Content.Headers.ContentType?.MediaType);
        Assert.Equal(body, await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static Task AssertGeocodingUnavailable(HttpResponseMessage r) =>
        AssertBody(r, HttpStatusCode.ServiceUnavailable, "application/problem+json",
            "{\"title\":\"Geocoding Unavailable\",\"status\":503,\"detail\":\"" + GeocodingDetail + "\",\"retryAfterSeconds\":15}");

    private static MultipartFormDataContent Csv(string text)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { file, "file", "addresses.csv" } };
    }

    // ---- 400 { error }: real services, so the message is exactly what a client receives -------------

    [Fact]
    public async Task Dbscan_WithNoPoints_Returns400ErrorBody()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var r = await f.Client().PostAsJsonAsync(
            "/api/v1/clustering/dbscan", new DbscanRequestDto(), TestContext.Current.CancellationToken);

        // Assert
        await AssertBody(r, HttpStatusCode.BadRequest, "application/json",
            "{\"error\":\"At least one point is required. (Parameter 'request')\"}");
    }

    [Fact]
    public async Task ReverseGeocode_LatitudeOutOfRange_Returns400ErrorBody()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var r = await f.Client().GetAsync("/api/v1/geocoding/reverse?lat=95&lng=0", TestContext.Current.CancellationToken);

        // Assert
        await AssertBody(r, HttpStatusCode.BadRequest, "application/json",
            "{\"error\":\"Latitude must be between -90 and 90 (Parameter 'latitude')\"}");
        Assert.Equal(0, f.ArcGisAttempts("IReverseGeocodingService"));
    }

    [Fact]
    public async Task BatchSync_HeaderOnlyCsv_Returns400ErrorBody()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        using var body = Csv("Id,Address,City,State,Zip\n");

        // Act
        var r = await f.Client().PostAsync("/api/v1/geocoding/batch/sync", body, TestContext.Current.CancellationToken);

        // Assert
        await AssertBody(r, HttpStatusCode.BadRequest, "application/json",
            "{\"error\":\"The CSV file contains no data rows. (Parameter 'addresses.csv')\"}");
        Assert.Equal(0, f.ArcGisAttempts("IBatchGeocodingService"));
    }

    // ---- 503 ProblemDetails: the breaker exception, from a stubbed service ------------------------

    [Fact]
    public async Task ReverseGeocode_BreakerOpen_Returns503GeocodingBody()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var service = new Mock<IReverseGeocodingService>();
        service.Setup(s => s.ReverseGeocodeAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BrokenCircuitException("open"));
        var client = ClientWith(f, s => s.AddSingleton(service.Object));

        // Act
        var r = await client.GetAsync("/api/v1/geocoding/reverse?lat=34&lng=-117", TestContext.Current.CancellationToken);

        // Assert
        await AssertGeocodingUnavailable(r);
    }

    [Theory]
    [InlineData("/api/v1/addresses/parse")]
    [InlineData("/api/v1/addresses/validate")]
    public async Task Address_BreakerOpen_Returns503GeocodingBody(string url)
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var service = new Mock<IAddressStandardizationService>();
        service.Setup(s => s.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BrokenCircuitException("open"));
        service.Setup(s => s.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BrokenCircuitException("open"));
        var client = ClientWith(f, s => s.AddSingleton(service.Object));

        // Act
        var r = await client.PostAsJsonAsync(
            url, new AddressParseRequestDto { RawAddress = "1 Main St, Redlands CA" }, TestContext.Current.CancellationToken);

        // Assert
        await AssertGeocodingUnavailable(r);
    }

    [Theory]
    [InlineData("/api/v1/geocoding/batch")]
    [InlineData("/api/v1/geocoding/batch/sync")]
    public async Task Batch_BreakerOpen_Returns503GeocodingBody(string url)
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var service = new Mock<IBatchGeocodingService>();
        service.Setup(s => s.EnqueueAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BrokenCircuitException("open"));
        service.Setup(s => s.GeocodeAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BrokenCircuitException("open"));
        var client = ClientWith(f, s => s.AddSingleton(service.Object));
        using var body = Csv("Id,Address,City,State,Zip\n1,1 Main St,Redlands,CA,92373\n");

        // Act
        var r = await client.PostAsync(url, body, TestContext.Current.CancellationToken);

        // Assert
        await AssertGeocodingUnavailable(r);
    }

    [Fact]
    public async Task BatchSubmit_QueueFull_Returns503QueueFullBodyWithRetryAfterHeader()
    {
        // Arrange: the queue-full branch stays in the action; it must still win over the filters.
        await using var f = new PortfolioAppFactory();
        var service = new Mock<IBatchGeocodingService>();
        service.Setup(s => s.EnqueueAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BatchQueueFullException());
        var client = ClientWith(f, s => s.AddSingleton(service.Object));
        using var body = Csv("Id,Address,City,State,Zip\n1,1 Main St,Redlands,CA,92373\n");

        // Act
        var r = await client.PostAsync("/api/v1/geocoding/batch", body, TestContext.Current.CancellationToken);

        // Assert
        await AssertBody(r, HttpStatusCode.ServiceUnavailable, "application/problem+json",
            "{\"title\":\"Batch Queue Full\",\"status\":503,\"detail\":\"Too many batch jobs are queued. Retry after 30 seconds.\",\"retryAfterSeconds\":30}");
        Assert.Equal("30", Assert.Single(r.Headers.GetValues("Retry-After")));
    }

    [Fact]
    public async Task Features_BreakerOpen_Returns503FeatureServiceBody()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var service = new Mock<IArcGisService>();
        service.Setup(s => s.QueryFeaturesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BrokenCircuitException("open"));
        var client = ClientWith(f, s => s.AddSingleton(service.Object));

        // Act
        var r = await client.GetAsync("/api/v1/features?layerId=0", TestContext.Current.CancellationToken);

        // Assert
        await AssertBody(r, HttpStatusCode.ServiceUnavailable, "application/problem+json",
            "{\"title\":\"Feature Service Unavailable\",\"status\":503,"
            + "\"detail\":\"The upstream GIS feature service is temporarily unavailable. Retry after 30 seconds.\","
            + "\"retryAfterSeconds\":30}");
    }
}
