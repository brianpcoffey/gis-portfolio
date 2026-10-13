using System.Net;
using System.Text;

namespace Portfolio.E2E.Infrastructure;

/// <summary>
/// The transport under the server-side <c>IArcGisService</c> client in the E2E host: it answers the
/// Census layer-3 (states) query from a canned FeatureSet and returns 404 for everything else.
/// </summary>
/// <remarks>
/// Nothing here touches the network. State Explorer asks for <c>GET /api/v1/features?layerId=3</c> on load, and
/// the integration-test factory's blanket 503 would make that page log a console error. The three geocoding
/// clients keep the factory's 503 fake, so no E2E request can reach ArcGIS geocoding.
/// </remarks>
internal sealed class ArcGisFixtureHandler(string layer3Json) : HttpMessageHandler
{
    internal const string FixtureFileName = "features-layer3.json";

    /// <summary>Reads the canned FeatureSet that is copied next to the test assembly.</summary>
    public static ArcGisFixtureHandler FromFixtureFile() =>
        new(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", FixtureFileName)));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isLayer3Query = request.Method == HttpMethod.Get &&
                            request.RequestUri?.AbsolutePath.EndsWith("/3/query", StringComparison.Ordinal) == true;

        var response = isLayer3Query
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(layer3Json, Encoding.UTF8, "application/json")
            }
            : new HttpResponseMessage(HttpStatusCode.NotFound);

        return Task.FromResult(response);
    }
}
