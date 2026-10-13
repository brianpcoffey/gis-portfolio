using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace Portfolio.E2E.Infrastructure;

/// <summary>What the E2E network policy does with one browser request.</summary>
public enum RouteKind
{
    /// <summary>The app under test. Continues, with a per-test <c>X-Forwarded-For</c>.</summary>
    App,

    /// <summary>An allowed third-party host (ArcGIS, the pinned Leaflet path). Continues to the real CDN.</summary>
    Cdn,

    /// <summary>An OpenStreetMap tile. Answered with a 1x1 PNG; the tile servers are never contacted.</summary>
    OsmTile,

    /// <summary>An ArcGIS geocoding endpoint. Aborted and recorded; any hit fails the test.</summary>
    Geocoding,

    /// <summary>Anything else. Aborted and recorded; any hit fails the test.</summary>
    Unexpected
}

/// <summary>
/// The network rules for one browser context (decision D-05), installed as a single catch-all route so the
/// order below is the order of the code.
/// </summary>
/// <remarks>
/// <list type="number">
/// <item><description>Same origin as the app: continue, adding <c>X-Forwarded-For: 198.18.a.b</c>, unique per
/// context. The host trusts that header in Development, so the 300/min global, 15/min "expensive" and
/// "10 new anonymous identities per minute" limits are partitioned per test instead of shared by all of them.</description></item>
/// <item><description>ArcGIS geocoding (<c>geocode*.arcgis.com</c>, any <c>/GeocodeServer</c> path): abort and record.
/// This is checked before the ArcGIS allow rule, which would otherwise let it through. The repo rule is that
/// no test may reach ArcGIS geocoding.</description></item>
/// <item><description>OpenStreetMap tiles: fulfil with a 1x1 PNG (the OSM tile usage policy forbids bulk automated
/// fetching, and a stub is deterministic).</description></item>
/// <item><description><c>js.arcgis.com</c>, other <c>*.arcgis.com</c> / <c>*.arcgisonline.com</c> hosts and the pinned
/// <c>unpkg.com/leaflet@1.9.4/</c> path: continue to the real CDN, so the SRI pins are really checked.</description></item>
/// <item><description>Everything else: abort and record in <see cref="UnexpectedRequests"/>.</description></item>
/// </list>
/// </remarks>
public sealed class NetworkPolicy
{
    // The Leaflet path the CSP and the pages pin. A new Leaflet version is a deliberate change here too.
    private const string LeafletPathPrefix = "/leaflet@1.9.4/";

    // A valid 1x1 opaque light-gray PNG (RGB 229,229,229): a stubbed tile is visibly different from a broken image.
    private static readonly byte[] TilePng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR42mN4+vQpAAViArCvQgNAAAAAAElFTkSuQmCC");

    private static int _nextPartition;

    private readonly Uri _appOrigin;
    private readonly string _forwardedFor;
    private readonly ConcurrentQueue<string> _unexpected = new();
    private readonly ConcurrentQueue<string> _geocoding = new();

    public NetworkPolicy(Uri appOrigin)
    {
        _appOrigin = appOrigin;
        _forwardedFor = NextForwardedFor();
    }

    /// <summary>The <c>X-Forwarded-For</c> value this context sends to the app.</summary>
    public string ForwardedFor => _forwardedFor;

    /// <summary>Requests to hosts the policy does not know (aborted), as <c>METHOD scheme://host/path</c>.</summary>
    public IReadOnlyList<string> UnexpectedRequests => [.. _unexpected];

    /// <summary>Requests that tried to reach ArcGIS geocoding (aborted), as <c>METHOD scheme://host/path</c>.</summary>
    public IReadOnlyList<string> GeocodingRequests => [.. _geocoding];

    /// <summary>Routes every request of <paramref name="context"/> through this policy.</summary>
    public Task InstallAsync(IBrowserContext context) => context.RouteAsync("**/*", HandleAsync);

    /// <summary>Decides what to do with <paramref name="url"/>. Pure, so the table can be tested without a browser.</summary>
    public static RouteKind Classify(Uri url, Uri appOrigin)
    {
        if (IsSameOrigin(url, appOrigin))
            return RouteKind.App;

        var host = url.Host.ToLowerInvariant();

        if (IsGeocoding(host, url.AbsolutePath))
            return RouteKind.Geocoding;

        if (url.Scheme != Uri.UriSchemeHttps)
            return RouteKind.Unexpected;

        if (host == "tile.openstreetmap.org" || host.EndsWith(".tile.openstreetmap.org", StringComparison.Ordinal))
            return RouteKind.OsmTile;

        if (IsArcGisHost(host) ||
            (host == "unpkg.com" && url.AbsolutePath.StartsWith(LeafletPathPrefix, StringComparison.Ordinal)))
        {
            return RouteKind.Cdn;
        }

        return RouteKind.Unexpected;
    }

    private static bool IsSameOrigin(Uri url, Uri origin) =>
        string.Equals(url.Scheme, origin.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(url.Host, origin.Host, StringComparison.OrdinalIgnoreCase) &&
        url.Port == origin.Port;

    private static bool IsGeocoding(string host, string path) =>
        (host.StartsWith("geocode", StringComparison.Ordinal) && host.EndsWith(".arcgis.com", StringComparison.Ordinal)) ||
        path.Contains("/GeocodeServer", StringComparison.OrdinalIgnoreCase);

    private static bool IsArcGisHost(string host) =>
        host is "arcgis.com" or "arcgisonline.com" ||
        host.EndsWith(".arcgis.com", StringComparison.Ordinal) ||
        host.EndsWith(".arcgisonline.com", StringComparison.Ordinal);

    // 198.18.0.0/15 is reserved for benchmarking, so these can never collide with a real client address.
    private static string NextForwardedFor()
    {
        var n = Interlocked.Increment(ref _nextPartition);
        return $"198.18.{n / 250 % 250}.{n % 250 + 1}";
    }

    private async Task HandleAsync(IRoute route)
    {
        try
        {
            var request = route.Request;
            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var url))
            {
                _unexpected.Enqueue($"{request.Method} {request.Url}");
                await route.AbortAsync();
                return;
            }

            switch (Classify(url, _appOrigin))
            {
                case RouteKind.App:
                    await route.ContinueAsync(new RouteContinueOptions
                    {
                        Headers = new Dictionary<string, string>(request.Headers) { ["x-forwarded-for"] = _forwardedFor }
                    });
                    break;
                case RouteKind.Cdn:
                    await route.ContinueAsync();
                    break;
                case RouteKind.OsmTile:
                    await route.FulfillAsync(new RouteFulfillOptions
                    {
                        Status = 200,
                        ContentType = "image/png",
                        BodyBytes = TilePng,
                        Headers = new Dictionary<string, string> { ["access-control-allow-origin"] = "*" }
                    });
                    break;
                case RouteKind.Geocoding:
                    _geocoding.Enqueue(Describe(request.Method, url));
                    await route.AbortAsync();
                    break;
                default:
                    _unexpected.Enqueue(Describe(request.Method, url));
                    await route.AbortAsync();
                    break;
            }
        }
        catch (PlaywrightException)
        {
            // The page or context closed while the request was in flight (test teardown); nothing to answer.
        }
    }

    // Origin and path only: a query string can carry coordinates or tokens and is not needed to find the caller.
    private static string Describe(string method, Uri url) => $"{method} {url.Scheme}://{url.Authority}{url.AbsolutePath}";
}
