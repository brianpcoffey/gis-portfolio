using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Portfolio.E2E.Infrastructure;
using Portfolio.Repositories;
using Portfolio.Tests.Integration;

[assembly: AssemblyFixture(typeof(E2EHost))]

namespace Portfolio.E2E.Infrastructure;

/// <summary>
/// The real app on a real Kestrel port, plus one headless Chromium, shared by every browser test in the
/// assembly (an xUnit v3 assembly fixture; decision D-07: no Playwright xUnit helper package).
/// </summary>
/// <remarks>
/// <para>
/// The host is <c>Program.cs</c> as it ships, started through <see cref="PortfolioAppFactory"/> (EF InMemory
/// instead of Npgsql, migrations skipped, every ArcGIS client faked), with three E2E-only changes:
/// a DbContext that tolerates the transaction the sign-in seed opens, the test-only sign-in
/// (<see cref="E2ESignIn"/>), and a canned FeatureSet for the features client (<see cref="ArcGisFixtureHandler"/>).
/// The factory returned by <c>WithWebHostBuilder</c> does not inherit <c>UseKestrel</c>, so Kestrel is switched
/// on for that derived factory.
/// </para>
/// <para>
/// Nothing starts unless <see cref="E2EGate.Enabled"/>: xUnit may construct the fixture even when every test
/// is skipped, and a plain <c>dotnet test</c> must not need Chromium.
/// </para>
/// </remarks>
public sealed class E2EHost : IAsyncLifetime
{
    private static readonly Regex EsriVersion = new("""var VERSION = "(?<v>[0-9.]+)";""", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex LeafletScript = new(@"https://unpkg\.com/leaflet@[0-9.]+/dist/leaflet\.js", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private PortfolioAppFactory? _root;
    private WebApplicationFactory<Program>? _host;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>The bound address of the app under test, e.g. <c>http://127.0.0.1:54321</c> (no trailing slash).</summary>
    public string BaseUrl { get; private set; } = string.Empty;

    /// <summary>The app's origin as a <see cref="Uri"/>.</summary>
    public Uri BaseUri => new(BaseUrl);

    /// <summary>The app's service provider, for seeding or reading state directly.</summary>
    public IServiceProvider Services => (_host ?? throw NotStarted()).Services;

    /// <summary>The shared headless Chromium. Create a context per test through <see cref="NewSessionAsync"/>.</summary>
    public IBrowser Browser => _browser ?? throw NotStarted();

    /// <summary>Why the third-party scripts could not be fetched at start-up (an outage), or <c>null</c> when they could.</summary>
    public string? CdnOutage { get; private set; }

    /// <summary>
    /// Set when a CDN answered but refused a pinned URL (403, 404, 410 ...): the version was retired or this client is
    /// blocked, which would break the production maps too. <c>null</c> when nothing was refused.
    /// </summary>
    public string? CdnRejection { get; private set; }

    public async ValueTask InitializeAsync()
    {
        if (!E2EGate.Enabled)
            return;

        _root = new PortfolioAppFactory();
        var database = "e2e-" + Guid.NewGuid();
        _host = _root.WithWebHostBuilder(builder =>
        {
            // UseKestrel(0) on the derived factory switches to Kestrel but, observed with Mvc.Testing 10.0.12, leaves
            // it on its default address (http://localhost:5000: the dev server's port). The explicit URL is what gets
            // a free loopback port.
            builder.UseUrls("http://127.0.0.1:0");
            builder.ConfigureTestServices(services => ConfigureServices(services, database));
        });

        // Not inherited from the root factory: it must be called on the derived one, before the first request.
        _host.UseKestrel(0);
        _host.StartServer();
        BaseUrl = _host.ClientOptions.BaseAddress.GetLeftPart(UriPartial.Authority);
        if (BaseUri.Host != "127.0.0.1" || BaseUri.Port == 0)
        {
            throw new InvalidOperationException(
                $"The E2E host bound {BaseUrl} instead of a free port on 127.0.0.1; it could collide with the dev server.");
        }

        await E2ESeed.RunAsync(_host.Services);
        await PreflightCdnAsync();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async ValueTask DisposeAsync()
    {
        // Each step runs even if an earlier one throws, so a browser that fails to close cannot leave Kestrel
        // (and its port) running.
        try
        {
            if (_browser is not null)
                await _browser.CloseAsync();
        }
        finally
        {
            try
            {
                _playwright?.Dispose();
            }
            finally
            {
                if (_root is not null)
                    await _root.DisposeAsync();
            }
        }
    }

    /// <summary>Opens a new isolated browser context (and page) for one test.</summary>
    public Task<BrowserSession> NewSessionAsync(BrowserSessionOptions? options = null) =>
        BrowserSession.StartAsync(this, options ?? new BrowserSessionOptions());

    /// <summary>
    /// Skips the calling test when the ArcGIS or Leaflet CDN was unreachable at start-up, and fails it when a CDN
    /// refused a pinned URL.
    /// </summary>
    /// <remarks>
    /// Only an outage (transport error, timeout, 5xx, 408, 429) becomes a skip. A retired or blocked URL fails, and
    /// so does a CDN file that CHANGED, through its SRI pin.
    /// </remarks>
    public void RequireCdn()
    {
        if (CdnRejection is not null)
        {
            Assert.Fail(
                $"A pinned third-party URL was refused, so the production maps would break too (this is not an outage): {CdnRejection}");
        }

        if (CdnOutage is not null)
            Assert.Skip($"CDN unreachable: {CdnOutage}");
    }

    private static void ConfigureServices(IServiceCollection services, string database)
    {
        // The sign-in seed runs in a transaction, which EF InMemory ignores with a warning that is an error by default.
        services.AddDbContext<PortfolioDbContext>(options => options
            .UseInMemoryDatabase(database)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        services.AddSingleton<IStartupFilter, E2ESignIn>();

        // Only the features client gets real answers; the three geocoding clients keep the factory's 503.
        services.AddHttpClient("IArcGisService")
            .ConfigurePrimaryHttpMessageHandler(ArcGisFixtureHandler.FromFixtureFile);
    }

    // Looks up the pinned third-party URLs from the app's own files, then does a plain GET of each. This runs
    // once, before any test, and only records what it saw: an outage (transport error, timeout, 5xx, 408, 429) is
    // a skip for the tests that need the CDN, any other refusal is a failure for them (see RequireCdn).
    private async Task PreflightCdnAsync()
    {
        using var local = _host!.CreateClient();
        var themeScript = await local.GetStringAsync("/js/esri-theme.js");
        var fleetPage = await local.GetStringAsync("/Projects/Fleet");

        var version = EsriVersion.Match(themeScript);
        var leaflet = LeafletScript.Match(fleetPage);
        if (!version.Success || !leaflet.Success)
        {
            throw new InvalidOperationException(
                "Could not read the pinned ArcGIS version from /js/esri-theme.js or the Leaflet script from /Projects/Fleet; update E2EHost.");
        }

        using var external = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        foreach (var url in new[] { $"https://js.arcgis.com/{version.Groups["v"].Value}/init.js", leaflet.Value })
        {
            try
            {
                using var response = await external.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                var answer = $"{url} answered {(int)response.StatusCode} {response.StatusCode}";
                switch (CdnPreflight.Classify(response.StatusCode))
                {
                    case CdnVerdict.Outage:
                        CdnOutage ??= answer;
                        break;
                    case CdnVerdict.Rejected:
                        CdnRejection ??= answer;
                        break;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                CdnOutage ??= $"{url} could not be fetched ({ex.GetType().Name})";
            }
        }
    }

    private static InvalidOperationException NotStarted() =>
        new($"The E2E host is not running. Set {E2EGate.Variable}=1 to run the browser tests.");
}
