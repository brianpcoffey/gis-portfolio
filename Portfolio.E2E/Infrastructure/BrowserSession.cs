using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Portfolio.E2E.Infrastructure;

/// <summary>How a test wants its browser context set up.</summary>
/// <param name="ColorScheme">The emulated <c>prefers-color-scheme</c>.</param>
/// <param name="Width">Viewport width in CSS pixels.</param>
/// <param name="Height">Viewport height in CSS pixels.</param>
public sealed record BrowserSessionOptions(ColorScheme ColorScheme = ColorScheme.Light, int Width = 1280, int Height = 800);

/// <summary>
/// One isolated browser context and page for one test, with the network policy installed and the page's
/// errors collected. Create it in the test class's <c>InitializeAsync</c> and dispose it in <c>DisposeAsync</c>:
/// only there is the test's outcome known, and a failed test keeps its trace and a full-page screenshot.
/// </summary>
public sealed class BrowserSession : IAsyncDisposable
{
    private readonly ConcurrentQueue<string> _consoleErrors = new();
    private readonly ConcurrentQueue<string> _pageErrors = new();
    private readonly ConcurrentQueue<string> _requestFailures = new();

    private BrowserSession(IBrowserContext context, IPage page, NetworkPolicy network)
    {
        Context = context;
        Page = page;
        Network = network;
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
                _consoleErrors.Enqueue($"{message.Text} [{message.Location}]");
        };
        page.PageError += (_, error) => _pageErrors.Enqueue(error);
        page.RequestFailed += (_, request) => _requestFailures.Enqueue($"{request.Method} {request.Url} {request.Failure}");
    }

    public IBrowserContext Context { get; }

    /// <summary>The page every test drives. Console errors and page errors are collected for this page only.</summary>
    public IPage Page { get; }

    public NetworkPolicy Network { get; }

    /// <summary>Console messages of type <c>error</c>.</summary>
    public IReadOnlyList<string> ConsoleErrors => [.. _consoleErrors];

    /// <summary>Uncaught exceptions in the page.</summary>
    public IReadOnlyList<string> PageErrors => [.. _pageErrors];

    /// <summary>Requests that failed in the browser (includes the ones the network policy aborted). Diagnostics only.</summary>
    public IReadOnlyList<string> RequestFailures => [.. _requestFailures];

    internal static async Task<BrowserSession> StartAsync(E2EHost host, BrowserSessionOptions options)
    {
        var context = await host.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = host.BaseUrl,
            ColorScheme = options.ColorScheme,
            ViewportSize = new ViewportSize { Width = options.Width, Height = options.Height },
            ServiceWorkers = ServiceWorkerPolicy.Block,
            Locale = "en-US"
        });

        var network = new NetworkPolicy(host.BaseUri);
        await network.InstallAsync(context);
        await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true, Sources = false });

        return new BrowserSession(context, await context.NewPageAsync(), network);
    }

    /// <summary>
    /// Signs in through the test-only endpoint (<see cref="E2ESignIn"/>), which runs the app's real cookie
    /// sign-in, then navigates to <paramref name="returnUrl"/> and returns that page's response.
    /// </summary>
    public async Task<IResponse?> SignInAsync(string returnUrl = "/")
    {
        var signIn = await Page.GotoAsync(E2ESignIn.SignInPath);
        if (signIn is null || !signIn.Ok)
            throw new InvalidOperationException($"E2E sign-in failed with HTTP {signIn?.Status}.");
        return await Page.GotoAsync(returnUrl);
    }

    /// <summary>
    /// Fails when the page logged a console error or threw, when any request went to a host the policy does not
    /// allow, or when anything tried to reach ArcGIS geocoding. The message lists every offender.
    /// </summary>
    /// <remarks>
    /// Disposing the session repeats the two network checks (not the console and page ones, which a test may
    /// legitimately expect) for a test that otherwise passed, so a test that forgets this call cannot reach a
    /// forbidden host unnoticed.
    /// </remarks>
    public void AssertClean()
    {
        var problems = new List<string>();
        AddSection(problems, "Console errors", ConsoleErrors);
        AddSection(problems, "Page errors", PageErrors);
        AddNetworkSections(problems);

        if (problems.Count > 0)
            Assert.Fail(string.Join(Environment.NewLine, problems));
    }

    public async ValueTask DisposeAsync()
    {
        var failed = TestContext.Current.TestState?.Result == TestResult.Failed;
        try
        {
            if (failed)
                await KeepFailureArtifactsAsync();
            else
                await Context.Tracing.StopAsync();
        }
        finally
        {
            await Context.CloseAsync();
        }

        // Backstop for a test that never called AssertClean. Skipped for a failed test: its own failure is the one
        // to report (the artifacts above already list these requests), and this must not replace it.
        if (!failed)
        {
            var problems = new List<string>();
            AddNetworkSections(problems);
            if (problems.Count > 0)
                Assert.Fail("Checked on dispose; call AssertClean() in the test to assert it where it happens." + Environment.NewLine + string.Join(Environment.NewLine, problems));
        }
    }

    private void AddNetworkSections(List<string> into)
    {
        AddSection(into, "Requests to unexpected hosts (aborted)", Network.UnexpectedRequests);
        AddSection(into, "Requests to ArcGIS geocoding (aborted; no test may reach it)", Network.GeocodingRequests);
    }

    // A failed test leaves artifacts/<test>.zip (open with `playwright show-trace`) and a full-page PNG, and prints
    // what the page logged, so the CI log alone says which hosts were hit.
    private async Task KeepFailureArtifactsAsync()
    {
        var directory = ArtifactDirectory();
        Directory.CreateDirectory(directory);
        var stem = Path.Combine(directory, SafeFileName(TestContext.Current.Test?.TestDisplayName ?? "test"));

        try
        {
            await Page.ScreenshotAsync(new PageScreenshotOptions { Path = stem + ".png", FullPage = true });
        }
        catch (PlaywrightException)
        {
            // The page may already be gone (crash or close); the trace below is still worth saving.
        }

        await Context.Tracing.StopAsync(new TracingStopOptions { Path = stem + ".zip" });

        var report = new List<string>();
        AddSection(report, "Console errors", ConsoleErrors);
        AddSection(report, "Page errors", PageErrors);
        AddSection(report, "Unexpected requests (aborted)", Network.UnexpectedRequests);
        AddSection(report, "Geocoding requests (aborted)", Network.GeocodingRequests);
        AddSection(report, "Failed requests", RequestFailures);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Browser artifacts: {stem}.zip, {stem}.png{Environment.NewLine}{string.Join(Environment.NewLine, report)}");
    }

    private static void AddSection(List<string> into, string title, IReadOnlyList<string> items)
    {
        if (items.Count > 0)
            into.Add($"{title} ({items.Count}):{Environment.NewLine}  - {string.Join(Environment.NewLine + "  - ", items)}");
    }

    // PORTFOLIO_E2E_ARTIFACTS wins; otherwise Portfolio.E2E/artifacts next to the project file (git-ignored, and the
    // folder the CI job uploads), found by walking up from bin/<config>/<tfm>.
    private static string ArtifactDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("PORTFOLIO_E2E_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured);

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Portfolio.E2E.csproj")))
                return Path.Combine(dir.FullName, "artifacts");
        }

        return Path.Combine(AppContext.BaseDirectory, "artifacts");
    }

    private static string SafeFileName(string name)
    {
        var safe = Regex.Replace(name, "[^A-Za-z0-9._-]+", "_", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim('_');
        return safe.Length > 150 ? safe[..150] : safe;
    }
}
