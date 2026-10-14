using System.Collections.Concurrent;
using Microsoft.Playwright;
using Portfolio.E2E.Infrastructure;

namespace Portfolio.E2E.Tests;

/// <summary>
/// The Home Finder "Save Search" dialog for a signed-in visitor: a search ranks homes and plots them, the dialog opens
/// with the suggested name selected, an empty name is refused in place, one Enter saves once, the saved search is listed,
/// and Escape closes each dialog and gives focus back to the button that opened it.
/// </summary>
public sealed class HomeFinderSavedSearchTests(E2EHost host) : BrowserTestBase(host)
{
    private const string SearchesPath = "/api/v1/homefinder/searches";
    private const string SearchName = "E2E weekend search";

    [E2EFact]
    public async Task SaveSearch_NamedAndConfirmedWithEnter_SavesOnceAndListsIt()
    {
        // Arrange
        await StartAsync();
        var posts = CountSearchPosts();
        await LoadHomeFinderAsync();

        // Act and assert, step by step: each step is the precondition of the next.
        await SearchAsync();
        var results = await Page.Locator("#resultsPanel [data-property-id]").CountAsync();
        Assert.InRange(results, 1, 10);
        // Every ranked home is also a marker on the map (plotted a moment after the cards are listed).
        await Page.WaitForFunctionAsync(
            "n => window.HomeFinder.state.graphicsLayer.graphics.length === n", results);

        await OpenSaveDialogAsync();
        var name = Page.Locator("#saveSearchName");
        Assert.True(
            await name.EvaluateAsync<bool>("input => input.value.length > 0 && input.selectionStart === 0 && input.selectionEnd === input.value.length"),
            "the suggested name should be selected, so typing replaces it");

        await name.FillAsync(string.Empty);
        await Page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(name).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-invalid"));
        await Assertions.Expect(name).ToHaveAttributeAsync("aria-invalid", "true");
        await Assertions.Expect(name).ToBeFocusedAsync();
        Assert.Empty(posts);

        await name.FillAsync(SearchName);
        var save = await Page.RunAndWaitForResponseAsync(
            () => Page.Keyboard.PressAsync("Enter"),
            response => response.Request.Method == "POST" && new Uri(response.Url).AbsolutePath == SearchesPath);
        await WaitForDialogToCloseAsync("saveSearchModal");

        // Assert
        Assert.Equal(201, save.Status);
        Assert.Single(posts);
        await Assertions.Expect(Page.Locator("#btnSaveSearch")).ToBeFocusedAsync();

        await Page.Locator("#btnLoadSearches").ClickAsync();
        await Assertions.Expect(Page.Locator("#savedSearchesBody .card", new PageLocatorOptions { HasText = SearchName })).ToHaveCountAsync(1);
        // Bootstrap moves focus into the dialog when it has finished opening, and only a key pressed inside it dismisses it.
        await Page.WaitForFunctionAsync("() => document.getElementById('savedSearchesModal').contains(document.activeElement)");
        await Page.Keyboard.PressAsync("Escape");
        await WaitForDialogToCloseAsync("savedSearchesModal");
        await Assertions.Expect(Page.Locator("#btnLoadSearches")).ToBeFocusedAsync();

        Session.AssertClean(ConsoleAllowList.ForPage(await EsriBaseUrlAsync()));
    }

    [E2EFact]
    public async Task SaveSearch_DialogDismissedWithEscape_ClosesWithoutSavingAndReturnsFocusToTheButton()
    {
        // Arrange
        await StartAsync();
        var posts = CountSearchPosts();
        await LoadHomeFinderAsync();
        await SearchAsync();
        await OpenSaveDialogAsync();

        // Act
        await Page.Keyboard.PressAsync("Escape");
        await WaitForDialogToCloseAsync("saveSearchModal");

        // Assert
        await Assertions.Expect(Page.Locator("#btnSaveSearch")).ToBeFocusedAsync();
        Assert.Empty(posts);
        Session.AssertClean(ConsoleAllowList.ForPage(await EsriBaseUrlAsync()));
    }

    private async Task LoadHomeFinderAsync()
    {
        // Returns once the map is ready and the page has wired its buttons: that happens in the microtasks right after
        // the map is ready, and the loader's last step waits for more animation frames than that takes.
        await Loader.LoadAsync(PageCatalog.Find("/Projects/HomeFinder"), signedIn: true);
    }

    private async Task SearchAsync()
    {
        await Page.Locator("#btnSearch").ClickAsync();
        await Page.Locator("#resultsPanel [data-property-id]").First.WaitForAsync();
        await Page.WaitForFunctionAsync("() => !document.getElementById('btnSaveSearch').disabled");
    }

    private async Task OpenSaveDialogAsync()
    {
        await Page.Locator("#btnSaveSearch").ClickAsync();
        // The field has focus once the dialog has finished opening (Bootstrap's shown event), not before.
        await Assertions.Expect(Page.Locator("#saveSearchName")).ToBeFocusedAsync();
    }

    // Bootstrap removes `show` when the hide transition has finished. The page moves focus back in its hidden handler, which
    // runs a moment later (after the backdrop has faded), so the tests check focus with a retrying assertion.
    private Task WaitForDialogToCloseAsync(string id) =>
        Page.WaitForFunctionAsync("id => !document.getElementById(id).classList.contains('show') && document.getElementById(id).style.display === 'none'", id);

    private ConcurrentQueue<string> CountSearchPosts()
    {
        var posts = new ConcurrentQueue<string>();
        Page.Request += (_, request) =>
        {
            if (request.Method == "POST" && new Uri(request.Url).AbsolutePath == SearchesPath)
                posts.Enqueue(request.Url);
        };
        return posts;
    }

    private Task<string?> EsriBaseUrlAsync() =>
        Page.EvaluateAsync<string?>("() => window.PortfolioEsri?.baseUrl ?? null");
}
