using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Portfolio.E2E.Infrastructure;

namespace Portfolio.E2E.Tests;

/// <summary>
/// The ArcGIS maps are optional: when the ArcGIS script never arrives, or arrives but its modules do not, the map shows an
/// accessible notice (<c>.esri-load-error</c>, <c>role="alert"</c>) and the rest of the page keeps working. The start-up limit
/// that produces that notice counts only the time the page is visible.
/// </summary>
public sealed class ArcGisFailureTests(E2EHost host) : BrowserTestBase(host)
{
    // How soon the notice must appear when the script itself fails (the failure is immediate; this is a ceiling).
    private const float NoticeTimeoutMs = 5_000;

    // The notice for a map that started loading and did not finish is shown after the 15 s limit in the page scripts.
    private const int MapReadyLimitMs = 15_000;

    // A script that fails to load logs the browser's own error for the aborted URL, and the page logs that the map
    // failed. These are the only console errors this scenario expects, and only here.
    private ConsoleAllowList ExpectedWhenArcGisIsMissing() => ConsoleAllowList.None
        .Allow(
            "The browser reports each aborted ArcGIS request (init.js, the theme stylesheet) once, at that URL.",
            textContains: "Failed to load resource",
            locationStartsWith: "https://js.arcgis.com/")
        .Allow(
            "The page's own catch block logs why the map is missing before it shows the notice.",
            textStartsWith: "Map failed to load",
            locationStartsWith: Host.BaseUrl);

    /// <summary>The four pages with an ArcGIS map, the container the notice goes in, and a control that does not need the map.</summary>
    public static TheoryData<string, string> MapPages => new()
    {
        { "/Projects/HomeFinder", "#homeFinderMap" },
        { "/Projects/StateExplorer", "#mapView" },
        { "/Projects/ReverseGeocoding", "#rgMapView" },
        { "/Projects/PlantOperationsDashboard", "#plantOpsMap" }
    };

    [E2ETheory]
    [MemberData(nameof(MapPages))]
    public async Task MapPage_ArcGisScriptBlocked_ShowsTheNoticeAndTheRestOfThePageWorks(string path, string mapContainer)
    {
        // Arrange: js.arcgis.com is unreachable (the page-level route wins over the context's network policy).
        await StartAsync();
        await Page.RouteAsync("**/js.arcgis.com/**", route => route.AbortAsync());

        // Act
        await OpenAsync(path);
        var notice = Page.Locator($"{mapContainer} .esri-load-error[role=alert]");
        await notice.WaitForAsync(new LocatorWaitForOptions { Timeout = NoticeTimeoutMs });
        // The page wires its other controls right after it has shown the notice (microtasks); two animation frames
        // are more than enough for that.
        await Page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve(true))))");

        // Assert
        Assert.Contains("The map could not be loaded", await notice.InnerTextAsync(), StringComparison.Ordinal);
        await AssertNonMapControlWorksAsync(path);
        Session.AssertClean(ExpectedWhenArcGisIsMissing());
    }

    [E2EFact]
    public async Task VisibleTimeout_PageHiddenAndShownAgain_CountsOnlyTheVisibleTime()
    {
        // Arrange: a page with no map and no network need; the script under test is loaded into it. The clock is faked
        // before the first navigation and paused, so time moves only when the test says so.
        await StartAsync();
        await Page.Clock.InstallAsync(new ClockInstallOptions { TimeDate = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc) });
        await Page.GotoAsync("/Privacy");
        await Page.AddScriptTagAsync(new PageAddScriptTagOptions { Url = "/js/esri-theme.js" });
        await Page.Clock.PauseAtAsync(new DateTime(2026, 10, 13, 12, 1, 0, DateTimeKind.Utc));
        await Page.EvaluateAsync("() => { window.timeoutFired = 0; window.PortfolioEsri.visibleTimeout(15000, () => { window.timeoutFired++; }); }");

        // Act and assert: playwright's clock does not fake document.visibilityState, so the page's visibility is set in-page.
        await Page.Clock.RunForAsync(10_000);
        Assert.Equal(0, await FiredAsync());

        await SetVisibilityAsync("hidden");
        await Page.Clock.RunForAsync(60_000);
        Assert.Equal(0, await FiredAsync());

        await SetVisibilityAsync("visible");
        await Page.Clock.RunForAsync(4_999);
        Assert.Equal(0, await FiredAsync());

        await Page.Clock.RunForAsync(2);
        Assert.Equal(1, await FiredAsync());
        Session.AssertClean();
    }

    [E2EFact]
    public async Task HomeFinder_ArcGisModulesNeverArrive_ShowsTheNoticeAfterTheFifteenSecondLimit()
    {
        // Arrange: init.js loads for real, then the modules it asks for (esri/Map.js and the rest) hang for good. This is the
        // case the limit exists for: the loader may never call its error callback when a module fetch hangs.
        Host.RequireCdn();
        await StartAsync();
        var hung = 0;
        await Page.RouteAsync("**/js.arcgis.com/*/esri/**/*.js", route =>
        {
            Interlocked.Increment(ref hung);
            return Task.CompletedTask; // neither continued nor fulfilled nor aborted: the request never completes
        });

        // Act
        await OpenAsync("/Projects/HomeFinder");
        // The limit starts when the script has loaded and the page asks for its modules.
        await Page.WaitForFunctionAsync("() => typeof window.require === 'function'");
        var clock = Stopwatch.StartNew();
        var notice = Page.Locator("#homeFinderMap .esri-load-error[role=alert]");
        await notice.WaitForAsync(new LocatorWaitForOptions { Timeout = MapReadyLimitMs + 15_000 });
        var waited = clock.ElapsedMilliseconds;

        // Assert
        Assert.True(Volatile.Read(ref hung) > 0, "the route never matched a module request, so nothing was hung");
        // A little under the limit allows for the moment between the script loading and this test noticing it.
        Assert.InRange(waited, MapReadyLimitMs - 1_500, MapReadyLimitMs + 15_000);
        await Assertions.Expect(Page.Locator("#mapToolbar")).ToHaveClassAsync(new Regex("d-none"));
        Session.AssertClean(ExpectedWhenArcGisIsMissing());
    }

    // The page is opened without waiting for its map: that is what these scenarios are about. A hung ArcGIS module request
    // also holds the browser's load event back (the loader inserts script elements), so navigation ends at DOMContentLoaded.
    // PlantOps needs the test sign-in.
    private async Task OpenAsync(string path)
    {
        if (PageCatalog.Find(path).SignedIn)
            await Session.SignInAsync(path);
        else
            await Page.GotoAsync(path, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
    }

    // What still works without the map, per page.
    private async Task AssertNonMapControlWorksAsync(string path)
    {
        switch (path)
        {
            case "/Projects/HomeFinder":
                await Page.Locator("#btnSearch").ClickAsync();
                await Page.Locator("#resultsPanel [data-property-id]").First.WaitForAsync();
                break;
            case "/Projects/StateExplorer":
                // The three canned states are counted once the data has loaded without a map.
                await Assertions.Expect(Page.Locator("#statTotal")).ToHaveTextAsync("3");
                break;
            case "/Projects/ReverseGeocoding":
                // The recent lookups are listed even though the map is not.
                await Assertions.Expect(Page.Locator("#rgHistoryEmpty")).ToBeVisibleAsync();
                break;
            case "/Projects/PlantOperationsDashboard":
                await Assertions.Expect(Page.Locator("#plantOpsOrdersTable tbody tr").First).ToBeVisibleAsync();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(path), path, "No non-map control is defined for this page.");
        }
    }

    private async Task<int> FiredAsync() => await Page.EvaluateAsync<int>("() => window.timeoutFired");

    private Task SetVisibilityAsync(string state) =>
        Page.EvaluateAsync(
            "state => { Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => state }); document.dispatchEvent(new Event('visibilitychange')); }",
            state);
}
