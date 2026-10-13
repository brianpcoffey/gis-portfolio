using Microsoft.Playwright;

namespace Portfolio.E2E.Infrastructure;

/// <summary>
/// The lifetime every scenario class shares: a test opens its browser session with <see cref="StartAsync"/> (in its
/// arrange step, once it knows which colour scheme and width it needs), and the session is disposed after the test,
/// when its outcome is known, so a failed test keeps its trace and screenshot (see <see cref="BrowserSession"/>).
/// </summary>
public abstract class BrowserTestBase(E2EHost host) : IAsyncLifetime
{
    // Null when the test never started a session (or failed while starting it); DisposeAsync tolerates that so the
    // original failure shows.
    private BrowserSession? _session;
    private PageLoader? _loader;

    protected E2EHost Host { get; } = host;

    /// <summary>The test's session. Available after <see cref="StartAsync"/>.</summary>
    protected BrowserSession Session => _session ?? throw new InvalidOperationException("Call StartAsync() first.");

    /// <summary>The test's page. Available after <see cref="StartAsync"/>.</summary>
    protected IPage Page => Session.Page;

    /// <summary>Opens pages and waits for them. Available after <see cref="StartAsync"/>.</summary>
    protected PageLoader Loader => _loader ?? throw new InvalidOperationException("Call StartAsync() first.");

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => _session?.DisposeAsync() ?? ValueTask.CompletedTask;

    /// <summary>Opens the test's browser context and page with the network policy installed.</summary>
    protected async Task StartAsync(BrowserSessionOptions? options = null)
    {
        _session = await Host.NewSessionAsync(options);
        _loader = new PageLoader(Host, _session);
    }
}
