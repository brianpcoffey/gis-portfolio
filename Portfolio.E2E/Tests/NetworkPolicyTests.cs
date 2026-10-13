using Portfolio.E2E.Infrastructure;

namespace Portfolio.E2E.Tests;

/// <summary>
/// The routing table behind the browser tests. It needs no browser, so it always runs: a mistake here could let a
/// test reach ArcGIS geocoding, which the repo forbids.
/// </summary>
public class NetworkPolicyTests
{
    private static readonly Uri App = new("http://127.0.0.1:5000");

    [Theory]
    [InlineData("http://127.0.0.1:5000/Projects/HomeFinder", RouteKind.App)]
    [InlineData("http://127.0.0.1:5000/api/v1/features?layerId=3", RouteKind.App)]
    [InlineData("http://127.0.0.1:5001/", RouteKind.Unexpected)]
    [InlineData("http://localhost:5000/", RouteKind.Unexpected)]
    [InlineData("https://js.arcgis.com/4.29/init.js", RouteKind.Cdn)]
    [InlineData("https://js.arcgis.com/4.29/esri/themes/light/main.css", RouteKind.Cdn)]
    [InlineData("https://basemaps.arcgis.com/arcgis/rest/services/World_Basemap_v2/VectorTileServer", RouteKind.Cdn)]
    [InlineData("https://services.arcgisonline.com/arcgis/rest/services/World_Imagery/MapServer/tile/3/2/1", RouteKind.Cdn)]
    [InlineData("https://unpkg.com/leaflet@1.9.4/dist/leaflet.js", RouteKind.Cdn)]
    [InlineData("https://unpkg.com/leaflet@1.9.3/dist/leaflet.js", RouteKind.Unexpected)]
    [InlineData("https://unpkg.com/another-package@1.0.0/index.js", RouteKind.Unexpected)]
    [InlineData("https://a.tile.openstreetmap.org/13/1403/3270.png", RouteKind.OsmTile)]
    [InlineData("https://tile.openstreetmap.org/13/1403/3270.png", RouteKind.OsmTile)]
    [InlineData("https://www.openstreetmap.org/copyright", RouteKind.Unexpected)]
    [InlineData("https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer/findAddressCandidates", RouteKind.Geocoding)]
    [InlineData("https://geocode-api.arcgis.com/arcgis/rest/services/World/GeocodeServer/suggest", RouteKind.Geocoding)]
    [InlineData("https://services.arcgis.com/abc/arcgis/rest/services/Locator/GeocodeServer/reverseGeocode", RouteKind.Geocoding)]
    [InlineData("https://utility.arcgis.com/usrsvcs/servers/xyz/rest/services/World/GEOCODESERVER/suggest", RouteKind.Geocoding)]
    [InlineData("http://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer", RouteKind.Geocoding)]
    [InlineData("http://js.arcgis.com/4.29/init.js", RouteKind.Unexpected)]
    [InlineData("https://arcgis.com.evil.test/x", RouteKind.Unexpected)]
    [InlineData("https://evilarcgis.com/x", RouteKind.Unexpected)]
    [InlineData("https://fonts.googleapis.com/css2?family=Inter", RouteKind.Unexpected)]
    [InlineData("https://developers.google.com/identity/images/g-logo.png", RouteKind.Unexpected)]
    public void Classify_Url_ReturnsTheExpectedKind(string url, RouteKind expected)
    {
        // Arrange
        var uri = new Uri(url);

        // Act
        var kind = NetworkPolicy.Classify(uri, App);

        // Assert
        Assert.Equal(expected, kind);
    }

    [Fact]
    public void ForwardedFor_TwoPolicies_AreDistinctAndInTheBenchmarkingRange()
    {
        // Arrange
        var first = new NetworkPolicy(App);
        var second = new NetworkPolicy(App);

        // Act
        var addresses = new[] { first.ForwardedFor, second.ForwardedFor };

        // Assert
        Assert.NotEqual(first.ForwardedFor, second.ForwardedFor);
        Assert.All(addresses, a => Assert.StartsWith("198.18.", a, StringComparison.Ordinal));
    }
}
