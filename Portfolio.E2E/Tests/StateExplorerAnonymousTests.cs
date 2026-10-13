using System.Collections.Concurrent;
using Portfolio.E2E.Infrastructure;

namespace Portfolio.E2E.Tests;

/// <summary>
/// An anonymous visitor to State Explorer never asks the server for collections: the server tells the page it is
/// signed out (<c>data-signed-in="false"</c>), and the page skips a request that could only answer 401 (which would log a
/// console error). A signed-in visitor does ask, which proves the counter below can see such a request at all.
/// </summary>
public sealed class StateExplorerAnonymousTests(E2EHost host) : BrowserTestBase(host)
{
    private const string CollectionsPath = "/api/v1/collections";

    // The end of app.js initialize(): the collections list is rendered last, after the features, the saved states and
    // the collections call (or the decision to skip it) have all settled. "Loading..." is the markup's placeholder.
    private const string StartUpFinished =
        "() => { const list = document.getElementById('collectionsList'); return list !== null && list.children.length > 0 && !/Loading/.test(list.textContent); }";

    [E2EFact]
    public async Task StateExplorer_LoadedAnonymously_SendsNoCollectionsRequest()
    {
        // Arrange
        await StartAsync();
        var collectionsRequests = CountCollectionsRequests();
        var entry = PageCatalog.Find("/Projects/StateExplorer");

        // Act
        await Loader.LoadAsync(entry);
        // The call is made (or skipped) in the same step as the features call, before the list below is rendered; waiting
        // for that DOM state, instead of an idle period, says the whole start-up has run.
        await Page.WaitForFunctionAsync(StartUpFinished);
        await Loader.WaitUntilQuietAsync();

        // Assert
        Assert.Equal("false", await Page.Locator(".state-explorer-page").GetAttributeAsync("data-signed-in"));
        Assert.Equal(1, await Page.Locator("#collectionsSignInHint").CountAsync());
        Assert.Empty(collectionsRequests);
        Session.AssertClean(ConsoleAllowList.ForPage(await EsriBaseUrlAsync()));
    }

    [E2EFact]
    public async Task StateExplorer_LoadedSignedIn_SendsTheCollectionsRequest()
    {
        // Arrange: the positive control for the test above.
        await StartAsync();
        var collectionsRequests = CountCollectionsRequests();
        var entry = PageCatalog.Find("/Projects/StateExplorer");

        // Act
        await Loader.LoadAsync(entry, signedIn: true);
        await Page.WaitForFunctionAsync(StartUpFinished);
        await Loader.WaitUntilQuietAsync();

        // Assert
        Assert.Equal("true", await Page.Locator(".state-explorer-page").GetAttributeAsync("data-signed-in"));
        Assert.Equal(0, await Page.Locator("#collectionsSignInHint").CountAsync());
        Assert.Single(collectionsRequests);
        Session.AssertClean(ConsoleAllowList.ForPage(await EsriBaseUrlAsync()));
    }

    // Counts what the page asks the server for under /api/v1/collections, from before the first navigation.
    private ConcurrentQueue<string> CountCollectionsRequests()
    {
        var requests = new ConcurrentQueue<string>();
        Page.Request += (_, request) =>
        {
            if (new Uri(request.Url).AbsolutePath.StartsWith(CollectionsPath, StringComparison.Ordinal))
                requests.Enqueue($"{request.Method} {request.Url}");
        };
        return requests;
    }

    private Task<string?> EsriBaseUrlAsync() =>
        Page.EvaluateAsync<string?>("() => window.PortfolioEsri?.baseUrl ?? null");
}
