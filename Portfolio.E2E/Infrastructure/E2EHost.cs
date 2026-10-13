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

    /// <summary>Why the third-party scripts could not be fetched at start-up, or <c>null</c> when they could.</summary>
    public string? CdnProblem { get; private set; }

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
        if (BaseUri.Port is 5000 or 80)
        {
            throw new InvalidOperationException(
                $"The E2E host bound {BaseUrl} instead of a free loopback port; it would collide with the dev server.");
        }

        await E2ESeed.RunAsync(_host.Services);
        CdnProblem = await CheckCdnAsync();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
            await _browser.CloseAsync();

        _playwright?.Dispose();

        if (_root is not null)
            await _root.DisposeAsync();
    }

    /// <summary>Opens a new isolated browser context (and page) for one test.</summary>
    public Task<BrowserSession> NewSessionAsync(BrowserSessionOptions? options = null) =>
        BrowserSession.StartAsync(this, options ?? new BrowserSessionOptions());

    /// <summary>Skips the calling test when the ArcGIS or Leaflet CDN was unreachable at start-up.</summary>
    /// <remarks>An outage becomes a skip; a CDN file that CHANGED still fails, through its SRI pin.</remarks>
    public void RequireCdn()
    {
        if (CdnProblem is not null)
            Assert.Skip($"CDN unreachable: {CdnProblem}");
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
    // once, before any test: a failure here is an outage (skip), never a reason to fail the build.
    private async Task<string?> CheckCdnAsync()
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
                if (!response.IsSuccessStatusCode)
                    return $"{url} answered {(int)response.StatusCode}";
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return $"{url} could not be fetched ({ex.GetType().Name})";
            }
        }

        return null;
    }

    private static InvalidOperationException NotStarted() =>
        new($"The E2E host is not running. Set {E2EGate.Variable}=1 to run the browser tests.");
}
