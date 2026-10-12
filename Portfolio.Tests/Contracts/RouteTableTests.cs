using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Tests.Helpers;
using Portfolio.Tests.Integration;

namespace Portfolio.Tests.Contracts;

/// <summary>
/// Guards "public routes don't change". The golden list is every controller action endpoint as
/// "VERB /path" (the API-version segment is shown as v1), taken from the real endpoint table. It
/// has 78 entries: the 77 versioned routes, three of which are hidden aliases (homefinder/score,
/// homefinder/properties/{id}, geocoding/batch/sync), plus the one unversioned legacy route
/// POST /api/batchgeocoding/sync that the same obsolete action also exposes. Page endpoints are not
/// listed. Every route string in wwwroot/js/api-config.js must also resolve to a controller route.
/// </summary>
public class RouteTableTests
{
    private const string Expected = """
        DELETE /api/v1/collections/{id:int}
        DELETE /api/v1/features/saved/{id}
        DELETE /api/v1/fiber/materials/{id}
        DELETE /api/v1/fiber/orders/{id}
        DELETE /api/v1/fiber/shipments/{id}
        DELETE /api/v1/homefinder/searches/{id:int}
        DELETE /api/v1/profile/claims/{type}
        DELETE /api/v1/users/me/claims/{type}
        DELETE /api/v1/users/{userId:guid}
        GET /api/v1/catrisk/book
        GET /api/v1/change/scene
        GET /api/v1/collections
        GET /api/v1/collections/{id:int}
        GET /api/v1/features
        GET /api/v1/features/saved
        GET /api/v1/fiber/dashboard/stats
        GET /api/v1/fiber/materials
        GET /api/v1/fiber/materials/{id}
        GET /api/v1/fiber/orders
        GET /api/v1/fiber/orders/{id}
        GET /api/v1/fiber/shipments
        GET /api/v1/fiber/shipments/{id}
        GET /api/v1/fleet/scenario
        GET /api/v1/geocoding/batch/{jobId}/status
        GET /api/v1/geocoding/reverse
        GET /api/v1/homefinder/properties/{id:int}
        GET /api/v1/homefinder/property/{id:int}
        GET /api/v1/homefinder/searches
        GET /api/v1/homefinder/searches/{id:int}
        GET /api/v1/network/graph
        GET /api/v1/outage/network
        GET /api/v1/profile
        GET /api/v1/response/scenario
        GET /api/v1/users/google/{googleId}
        GET /api/v1/users/me
        GET /api/v1/users/me/claims
        GET /api/v1/users/{userId:guid}
        POST /api/batchgeocoding/sync
        POST /api/v1/addresses/parse
        POST /api/v1/addresses/validate
        POST /api/v1/catrisk/accumulation
        POST /api/v1/catrisk/simulate
        POST /api/v1/change/detect
        POST /api/v1/clustering/dbscan
        POST /api/v1/collections
        POST /api/v1/features/saved
        POST /api/v1/fiber/materials
        POST /api/v1/fiber/materials/{id}/receive
        POST /api/v1/fiber/orders
        POST /api/v1/fiber/shipments
        POST /api/v1/fleet/optimize
        POST /api/v1/geocoding/batch
        POST /api/v1/geocoding/batch/sync
        POST /api/v1/geometry/clip
        POST /api/v1/geometry/triangulate
        POST /api/v1/geostream/events
        POST /api/v1/homefinder/score
        POST /api/v1/homefinder/search
        POST /api/v1/homefinder/searches
        POST /api/v1/network/route
        POST /api/v1/network/service-area
        POST /api/v1/outage/restore
        POST /api/v1/outage/trace
        POST /api/v1/overlay/spatial-join
        POST /api/v1/profile/claims
        POST /api/v1/raster/heatmap
        POST /api/v1/raster/hillshade
        POST /api/v1/response/isochrone
        POST /api/v1/response/optimize
        POST /api/v1/viewshed/compute
        PUT /api/v1/collections/{id:int}
        PUT /api/v1/fiber/materials/{id}
        PUT /api/v1/fiber/orders/{id}
        PUT /api/v1/fiber/shipments/{id}
        PUT /api/v1/fiber/shipments/{id}/status
        PUT /api/v1/profile/me/claims
        PUT /api/v1/users/me
        PUT /api/v1/users/me/claims
        """;

    private static List<string> ActionRoutes(PortfolioAppFactory f) =>
        f.Services.GetServices<EndpointDataSource>()
            .SelectMany(d => d.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["?"])
                .Select(m => $"{m} /{e.RoutePattern.RawText!.Replace("v{version:apiVersion}", "v1")}"))
            .Order(StringComparer.Ordinal)
            .ToList();

    [Fact]
    public async Task ControllerActionRoutes_MatchTheGoldenList()
    {
        await using var f = new PortfolioAppFactory();
        _ = f.Client();

        var expected = Expected.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        Assert.Equal(78, expected.Count);
        GoldenAssert.Equal(expected, ActionRoutes(f));
    }

    [Fact]
    public async Task EveryApiRouteInApiConfigJs_MatchesAControllerRoute()
    {
        await using var f = new PortfolioAppFactory();
        var js = await f.Client().GetStringAsync("/js/api-config.js", TestContext.Current.CancellationToken);
        var templates = ActionRoutes(f).Select(r => r[(r.IndexOf(' ') + 1)..]).ToList();

        // Entries look like  name : BASE + "/segment/segment"  with BASE = "/api/v1".
        var paths = Regex.Matches(js, @"BASE\s*\+\s*""(?<p>/[^""]+)""")
            .Select(m => "/api/v1" + m.Groups["p"].Value)
            .Distinct()
            .ToList();
        Assert.True(paths.Count >= 40, $"parsed only {paths.Count} routes from api-config.js");

        static Regex ToRegex(string template) =>
            new("^" + Regex.Replace(Regex.Escape(template), @"\\\{[^}]+\}", "[^/]+") + "$");

        var unmatched = paths.Where(p => !templates.Any(t =>
                ToRegex(t).IsMatch(p) ||                                                   // exact route
                (t.EndsWith('}') && ToRegex(t[..t.LastIndexOf('/')]).IsMatch(p))))         // base of a ".../{id}" route
            .ToList();

        Assert.Empty(unmatched);
    }
}
