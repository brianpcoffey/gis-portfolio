using System.Net;
using System.Text;

namespace Portfolio.Tests.Integration;

/// <summary>
/// The unversioned alias <c>POST /api/batchgeocoding/sync</c> is gone (it had answered 404 since API versioning
/// began requiring a URL version); only <c>POST /api/v1/geocoding/batch/sync</c> is live.
/// </summary>
public class LegacyBatchGeocodingRouteTests
{
    private static MultipartFormDataContent CsvBody()
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("Id,Address,City,State,Zip\n1,1 Main St,Redlands,CA,92373\n"));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { file, "file", "addresses.csv" } };
    }

    [Fact]
    public async Task PostUnversionedLegacyAlias_Returns404_WithoutCallingArcGis()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        using var client = f.Client();
        using var body = CsvBody();

        // Act
        var response = await client.PostAsync("/api/batchgeocoding/sync", body, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, f.ArcGisAttempts("arcgis-batch"));
        Assert.Equal(0, f.ArcGisAttempts("IBatchGeocodingService"));
    }

    [Fact]
    public async Task PostVersionedSync_IsStillRouted()
    {
        // Arrange: the fake ArcGIS transport answers 503, so a request that reaches the action cannot return 404.
        await using var f = new PortfolioAppFactory();
        using var client = f.Client();
        using var body = CsvBody();

        // Act
        var response = await client.PostAsync("/api/v1/geocoding/batch/sync", body, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}
