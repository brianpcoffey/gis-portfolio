using System.Net;
using System.Text;
using Portfolio.Tests.Helpers;

namespace Portfolio.Tests.Integration;

/// <summary>
/// WP-36 / D-10: both batch geocoding endpoints reject a CSV over 1,000 data rows, before any ArcGIS
/// call, with the public 400 <c>{ "error": ... }</c> body.
/// </summary>
public class BatchGeocodingBoundsTests
{
    private static MultipartFormDataContent CsvBody(int dataRows)
    {
        var sb = new StringBuilder("Id,Address,City,State,Zip\n");
        for (var i = 1; i <= dataRows; i++)
            sb.Append(i).Append(',').Append(i).Append(" Main St,Redlands,CA,92373\n");
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(sb.ToString()));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { file, "file", "addresses.csv" } };
    }

    [Theory]
    [InlineData("/api/v1/geocoding/batch")]
    [InlineData("/api/v1/geocoding/batch/sync")]
    public async Task Post_1001Rows_Returns400WithRowCapMessage(string url)
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        using var client = f.Client();
        using var body = CsvBody(1001);

        // Act
        var response = await client.PostAsync(url, body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("CSV exceeds the maximum of 1000 data rows.", text);
        Assert.Equal(0, f.ArcGisAttempts("arcgis-batch"));
    }
}
