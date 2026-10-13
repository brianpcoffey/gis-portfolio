namespace Portfolio.E2E.Infrastructure;

/// <summary>What a page needs before it counts as loaded.</summary>
public enum PageKind
{
    /// <summary>No third-party map: the page is loaded when the browser's load event has fired and its start-up requests are done.</summary>
    Static,

    /// <summary>An ArcGIS map: also needs the map's view surface, named by the page once the view is ready (or the page's load-error notice, which fails the test).</summary>
    ArcGis,

    /// <summary>A Leaflet map: also needs at least one (stubbed) tile in the DOM.</summary>
    Leaflet
}

/// <summary>One page of the site that the page-health, layout and accessibility suites visit.</summary>
/// <param name="Path">The app-relative URL.</param>
/// <param name="Kind">What "loaded" means for the page.</param>
/// <param name="SignedIn">True when the page is behind sign-in and is opened through the test-only sign-in.</param>
/// <param name="ReadyScript">
/// An optional JavaScript expression that is true once the page's own start-up has finished (for example a
/// flag the page sets), evaluated after the map is present. <c>null</c> when the map and the request check are enough.
/// </param>
public sealed record PageEntry(string Path, PageKind Kind, bool SignedIn = false, string? ReadyScript = null)
{
    /// <summary>True for the pages that load a map from a third-party CDN, i.e. the ones a CDN outage skips.</summary>
    public bool NeedsCdn => Kind is PageKind.ArcGis or PageKind.Leaflet;

    public override string ToString() => SignedIn ? $"{Path} (signed in)" : Path;
}

/// <summary>
/// Every page the browser suites cover: the 18 demos and their Details pages, the site pages, and the two pages that
/// need a signed-in visitor. <c>PageCatalogTests</c> fails when a demo is added to <c>Portfolio.Web/Pages/Projects</c>
/// and not listed here.
/// </summary>
public static class PageCatalog
{
    // Which demos use a map, and which one. Every other demo is a Static page.
    private static readonly Dictionary<string, PageKind> DemoKinds = new(StringComparer.Ordinal)
    {
        ["AddressStandardization"] = PageKind.Static,
        ["BatchGeocoding"] = PageKind.Static,
        ["CatRisk"] = PageKind.Static,
        ["Change"] = PageKind.Static,
        ["Clustering"] = PageKind.Static,
        ["Fleet"] = PageKind.Leaflet,
        ["GeoStream"] = PageKind.Static,
        ["Geometry"] = PageKind.Static,
        ["HomeFinder"] = PageKind.ArcGis,
        ["Network"] = PageKind.Leaflet,
        ["Outage"] = PageKind.Static,
        ["Overlay"] = PageKind.Static,
        ["PlantOperationsDashboard"] = PageKind.ArcGis,
        ["Response"] = PageKind.Leaflet,
        ["ReverseGeocoding"] = PageKind.ArcGis,
        ["StateExplorer"] = PageKind.ArcGis,
        ["Terrain"] = PageKind.Static,
        ["Viewshed"] = PageKind.Static
    };

    /// <summary>The folder names under <c>Pages/Projects</c> (the 18 demos).</summary>
    public static IReadOnlyCollection<string> DemoNames => DemoKinds.Keys;

    // Pages whose script finishes after the map exists and that publish no DOM marker of their own.
    private static string? ReadyScriptFor(string demo) => demo switch
    {
        // map.js builds the view, then app.js wires the buttons: the view's ready flag is the last observable step.
        "HomeFinder" => "window.HomeFinder?.state?.mapView?.ready === true",
        _ => null
    };

    /// <summary>All pages, in the order the suites list them.</summary>
    public static IReadOnlyList<PageEntry> All { get; } = Build();

    /// <summary>The paths of <see cref="All"/>, as xUnit theory data.</summary>
    public static TheoryData<string> Paths => [.. All.Select(p => p.Path)];

    /// <summary>The entry for <paramref name="path"/>.</summary>
    public static PageEntry Find(string path) =>
        All.Single(p => string.Equals(p.Path, path, StringComparison.Ordinal));

    private static List<PageEntry> Build()
    {
        var pages = new List<PageEntry>();

        foreach (var (name, kind) in DemoKinds)
        {
            // The dashboard is behind sign-in; its Details page is public.
            pages.Add(new PageEntry($"/Projects/{name}", kind, SignedIn: name == "PlantOperationsDashboard", ReadyScript: ReadyScriptFor(name)));
        }

        pages.AddRange(DemoKinds.Keys.Select(name => new PageEntry($"/Projects/{name}/Details", PageKind.Static)));

        pages.Add(new PageEntry("/", PageKind.Static));
        pages.Add(new PageEntry("/Projects", PageKind.Static));
        pages.Add(new PageEntry("/Login", PageKind.Static));
        pages.Add(new PageEntry("/Privacy", PageKind.Static));
        pages.Add(new PageEntry("/Accessibility", PageKind.Static));
        pages.Add(new PageEntry("/Account/Profile", PageKind.Static, SignedIn: true));

        return pages;
    }
}
