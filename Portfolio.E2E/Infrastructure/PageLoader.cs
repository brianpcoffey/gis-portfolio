using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace Portfolio.E2E.Infrastructure;

/// <summary>
/// Opens a page of the site and waits until it has finished starting up, using conditions that exist in the page
/// (never <c>NetworkIdle</c>, never a fixed sleep).
/// </summary>
/// <remarks>
/// <para>
/// "Started up" means: the browser's load event has fired; the map, if the page has one, is in the DOM (ArcGIS view
/// surface or a stubbed Leaflet tile); the page's own ready expression holds, if it has one; and the page has no
/// <c>fetch</c>/<c>XMLHttpRequest</c> call to the app in flight. The last one is what makes "no console errors" mean
/// something: a failing start-up request would otherwise log its error after the assertion had already passed.
/// </para>
/// <para>
/// Only the app's own fetch and XHR requests are counted. Map tiles and ArcGIS modules keep streaming for as long
/// as a map is on screen, so counting them would never reach zero; they are covered by the map-present condition.
/// </para>
/// </remarks>
public sealed class PageLoader
{
    // Ceilings, not waits: each condition below ends as soon as it holds. A cold CDN fetch plus software-rendered
    // WebGL on a small runner is slow, hence the generous map ceiling.
    private const float MapReadyTimeoutMs = 45_000;
    private const float PageReadyTimeoutMs = 15_000;
    private static readonly TimeSpan QuietTimeout = TimeSpan.FromSeconds(15);

    // How long two animation frames may take before WaitForFramesAsync stops waiting for them (normally about 33 ms).
    private const int FrameFallbackMs = 3_000;

    private readonly E2EHost _host;
    private readonly BrowserSession _session;
    private readonly ConcurrentDictionary<IRequest, string> _inFlight = new();
    private int _started;

    /// <summary>Starts counting the page's app requests. Create it before the first navigation.</summary>
    public PageLoader(E2EHost host, BrowserSession session)
    {
        _host = host;
        _session = session;

        session.Page.Request += (_, request) =>
        {
            if (IsAppCall(request))
            {
                _inFlight[request] = $"{request.Method} {new Uri(request.Url).AbsolutePath}";
                Interlocked.Increment(ref _started);
            }
        };
        session.Page.RequestFinished += (sender, request) => _inFlight.TryRemove(request, out _);
        session.Page.RequestFailed += (sender, request) => _inFlight.TryRemove(request, out _);
    }

    /// <summary>How many fetch/XHR calls to the app the page has started so far.</summary>
    public int AppCallsStarted => Volatile.Read(ref _started);

    /// <summary>The fetch/XHR calls to the app that are still in flight, as <c>METHOD /path</c>.</summary>
    public IReadOnlyList<string> AppCallsInFlight => [.. _inFlight.Values];

    /// <summary>
    /// Opens <paramref name="entry"/> and waits until it is ready. A page that needs sign-in is opened through the test-only
    /// sign-in; <paramref name="signedIn"/> signs a public page in as well (a page that behaves differently for a signed-in visitor).
    /// </summary>
    public async Task<IResponse?> LoadAsync(PageEntry entry, bool signedIn = false)
    {
        if (entry.NeedsCdn)
            _host.RequireCdn();

        var response = entry.SignedIn || signedIn
            ? await _session.SignInAsync(entry.Path)
            : await _session.Page.GotoAsync(entry.Path);

        await WaitUntilReadyAsync(entry);
        return response;
    }

    /// <summary>Waits for the map (if any), the page's ready expression (if any) and the end of the page's start-up requests.</summary>
    public async Task WaitUntilReadyAsync(PageEntry entry)
    {
        var page = _session.Page;

        switch (entry.Kind)
        {
            case PageKind.ArcGis:
                await WaitForArcGisMapAsync(page, entry);
                break;
            case PageKind.Leaflet:
                await page.WaitForSelectorAsync(
                    ".leaflet-container .leaflet-tile-loaded",
                    new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached, Timeout = MapReadyTimeoutMs });
                break;
        }

        if (entry.Kind != PageKind.ArcGis && entry.ReadyScript is not null)
            await WaitForExpressionAsync(page, entry.ReadyScript);

        // Web fonts (Font Awesome icons are glyphs) and images change the size of what they sit in. A measurement taken
        // before they arrive (layout, an axe colour check that depends on what overlaps what) can differ from one taken
        // after, so they are part of "ready".
        await WaitForExpressionAsync(
            page,
            "document.readyState === 'complete' && document.fonts.status === 'loaded' && Array.from(document.images).every(i => i.complete || i.loading === 'lazy')");

        // Entrance animations (the staggered rise of the cards on the profile page, the scroll fade-ins) run for a few hundred
        // milliseconds after load, and with reduced motion on only their duration is cut: their delays still apply, so a
        // card can sit offset and transparent until its turn. A colour or position read before they end is a mid-animation
        // value. Animations that never end (spinners, the home carousel) are not waited for.
        await WaitForExpressionAsync(
            page,
            "document.getAnimations().every(a => (a.playState !== 'running' && a.playState !== 'pending') || a.effect?.getComputedTiming().iterations === Infinity)");

        await WaitUntilQuietAsync();
    }

    /// <summary>
    /// Waits two animation frames, which is long enough for a response handler to start the call that follows it. A page that is
    /// not being rendered (hidden or throttled) fires no frames, so the wait also ends after <see cref="FrameFallbackMs"/> ms
    /// instead of hanging until the test run's own limit.
    /// </summary>
    public Task WaitForFramesAsync() =>
        _session.Page.EvaluateAsync(
            """
            ms => new Promise(resolve => {
                const timer = setTimeout(() => resolve(false), ms);
                requestAnimationFrame(() => requestAnimationFrame(() => { clearTimeout(timer); resolve(true); }));
            })
            """,
            FrameFallbackMs);

    /// <summary>
    /// Waits until no app call is in flight and none started during one more animation-frame round trip (which is
    /// long enough for a response handler to start the call that follows it). Fails, listing the stuck calls, when
    /// that does not happen within the ceiling.
    /// </summary>
    public async Task WaitUntilQuietAsync()
    {
        var deadline = DateTime.UtcNow + QuietTimeout;
        var quietRounds = 0;

        while (quietRounds < 2)
        {
            while (!_inFlight.IsEmpty)
            {
                if (DateTime.UtcNow > deadline)
                    Assert.Fail($"The page was still calling the app after {QuietTimeout.TotalSeconds:0} s: {string.Join(", ", AppCallsInFlight)}");
                await Task.Delay(10);
            }

            var startedBefore = AppCallsStarted;
            await WaitForFramesAsync();

            if (_inFlight.IsEmpty && AppCallsStarted == startedBefore)
            {
                quietRounds++;
            }
            else
            {
                quietRounds = 0;
                if (DateTime.UtcNow > deadline)
                    Assert.Fail($"The page kept calling the app for {QuietTimeout.TotalSeconds:0} s without settling ({AppCallsStarted} calls so far).");
            }
        }
    }

    // Ends with the map's view surface, or with the page's own failure notice (which fails the test at once and says
    // why, instead of timing out): a blocked script (a changed file fails its SRI pin) is found in seconds.
    // Every ArcGIS page names the surface (aria-label) in the callback of view.when(), i.e. once the view is ready,
    // and the calls that follow (for example the shipments the dashboard plots) start right after it. A surface
    // without the label is a map that is still starting.
    private static async Task WaitForArcGisMapAsync(IPage page, PageEntry entry)
    {
        var extra = entry.ReadyScript ?? "true";
        await page.WaitForFunctionAsync(
            $"() => document.querySelector('.esri-load-error') !== null || (document.querySelector('.esri-view-surface[aria-label]') !== null && ({extra}))",
            null,
            new PageWaitForFunctionOptions { Timeout = MapReadyTimeoutMs });

        var notice = page.Locator(".esri-load-error");
        if (await notice.CountAsync() > 0)
        {
            Assert.Fail(
                $"{entry.Path}: the map showed its load-error notice (the ArcGIS script or theme was blocked or failed to load): " +
                (await notice.First.InnerTextAsync()).Trim());
        }
    }

    private static Task WaitForExpressionAsync(IPage page, string expression) =>
        page.WaitForFunctionAsync($"() => {expression}", null, new PageWaitForFunctionOptions { Timeout = PageReadyTimeoutMs });

    // A call the page's script made to the app: fetch or XHR to the same origin. Page, script and stylesheet loads
    // are not calls, and neither is anything that goes to a CDN.
    private bool IsAppCall(IRequest request) =>
        request.ResourceType is "fetch" or "xhr" &&
        Uri.TryCreate(request.Url, UriKind.Absolute, out var url) &&
        url.Authority == _host.BaseUri.Authority;
}
