using Microsoft.Playwright;
using Portfolio.E2E.Infrastructure;

namespace Portfolio.E2E.Tests;

/// <summary>
/// The harness proving itself: the real app on a real port, driven by a real browser, with the network policy
/// and the test-only sign-in in place. The scenario suites (EN-02b) build on exactly these pieces.
/// </summary>
public sealed class SmokeTests(E2EHost host) : IAsyncLifetime
{
    // A cold CDN fetch plus software-rendered WebGL on a small runner is slow; this is a ceiling, not a wait.
    private const float MapReadyTimeoutMs = 45_000;

    private BrowserSession _session = null!;

    public async ValueTask InitializeAsync() => _session = await host.NewSessionAsync();

    public async ValueTask DisposeAsync() => await _session.DisposeAsync();

    [E2EFact]
    public async Task HomePage_LoadedSignedOut_RendersWithoutBrowserErrors()
    {
        // Arrange
        var page = _session.Page;

        // Act
        var response = await page.GotoAsync("/");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        Assert.Contains("Brian Coffey", await page.TitleAsync(), StringComparison.Ordinal);
        _session.AssertClean();
    }

    [E2EFact]
    public async Task HomeFinder_Loaded_MapReachesTheEsriViewSurface()
    {
        // Arrange
        host.RequireCdn();
        var page = _session.Page;

        // Act
        var response = await page.GotoAsync("/Projects/HomeFinder");
        // Either the map surface or the page's own failure notice ends the wait, so a blocked script (a changed
        // file fails its SRI pin) fails in seconds and says why, instead of timing out on a selector.
        await page.Locator("#homeFinderMap .esri-view-surface, #homeFinderMap .esri-load-error").First.WaitForAsync(
            new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = MapReadyTimeoutMs });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        Assert.True(
            await page.Locator("#homeFinderMap .esri-load-error").CountAsync() == 0,
            "The map showed its load-error notice: the ArcGIS script or theme was blocked or failed to load.");
        Assert.Equal(1, await page.Locator("#homeFinderMap .esri-view-surface").CountAsync());
        _session.AssertClean();
    }

    [E2EFact]
    public async Task Features_QueriedThroughTheApi_AreServedFromTheCannedFeatureSet()
    {
        // Arrange: a same-origin page, so the fetch goes through the network policy like any page request.
        var page = _session.Page;
        await page.GotoAsync("/");

        // Act
        var names = await page.EvaluateAsync<string[]>(
            "fetch('/api/v1/features?layerId=3').then(r => r.json()).then(features => features.map(f => f.name))");

        // Assert: the fixture, not the factory's blanket 503 (which the API would turn into an empty list).
        Assert.Equal(["Arizona", "California", "Nevada"], names);
        _session.AssertClean();
    }

    [E2EFact]
    public async Task SignIn_TestOnlyEndpoint_AuthenticatesThroughTheRealCookieScheme()
    {
        // Arrange
        var page = _session.Page;

        // Act: the profile page is behind the "Authenticated" policy, so landing on it proves the cookie works.
        var response = await _session.SignInAsync("/Account/Profile");
        var orders = await page.EvaluateAsync<int>(
            "fetch('/api/v1/fiber/orders').then(r => r.ok ? r.json() : Promise.reject(new Error('HTTP ' + r.status))).then(o => o.length)");
        var authCookie = (await _session.Context.CookiesAsync()).Single(c => c.Name == ".AspNetCore.Cookies");

        // Assert
        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        Assert.Equal("/Account/Profile", new Uri(page.Url).AbsolutePath);
        Assert.Equal("My Profile", await page.Locator("h1").InnerTextAsync());
        Assert.Equal("/", authCookie.Path); // an app.Map based sign-in would have scoped the cookie to /__e2e/signin
        Assert.True(orders > 0, "the real SignedIn path should have seeded the demo orders");
        _session.AssertClean();
    }
}
