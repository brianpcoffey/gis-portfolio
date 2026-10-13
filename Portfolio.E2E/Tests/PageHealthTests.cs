using Portfolio.E2E.Infrastructure;

namespace Portfolio.E2E.Tests;

/// <summary>
/// Every page of the site loads cleanly in a real browser: 200, no uncaught exception, no console error that is not on the
/// allow-list, no request to a host the network policy does not know, and (for the map pages) a map on screen.
/// </summary>
public sealed class PageHealthTests(E2EHost host) : BrowserTestBase(host)
{
    [E2ETheory]
    [MemberData(nameof(PageCatalog.Paths), MemberType = typeof(PageCatalog))]
    public async Task Page_Loaded_RendersWithoutBrowserErrors(string path)
    {
        // Arrange
        var entry = PageCatalog.Find(path);
        await StartAsync();

        // Act
        var response = await Loader.LoadAsync(entry);
        // The ArcGIS pages load esri-theme.js, which publishes the folder the API (and its CSP probe) comes from.
        var esriBaseUrl = await Page.EvaluateAsync<string?>("() => window.PortfolioEsri?.baseUrl ?? null");

        // Assert
        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        Assert.False(string.IsNullOrWhiteSpace(await Page.TitleAsync()), $"{path} has no <title>.");
        Assert.Equal(entry.Path, new Uri(Page.Url).AbsolutePath);
        Session.AssertClean(ConsoleAllowList.ForPage(esriBaseUrl));
    }
}
