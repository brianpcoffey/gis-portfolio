using System.Collections.Concurrent;
using Microsoft.Playwright;
using Portfolio.E2E.Infrastructure;

namespace Portfolio.E2E.Tests;

/// <summary>
/// The Home Finder "Save Search" dialog for a signed-in visitor: a search ranks homes and plots them, the dialog opens
/// with the suggested name selected, an empty name is refused in place, one Enter saves once, the saved search is listed,
/// and Escape closes each dialog. Escape on "Save Search" gives focus back to the button that opened it; on "Saved Searches"
/// it does not yet (a known issue: see <see cref="KnownIssues"/>).
/// </summary>
public sealed class HomeFinderSavedSearchTests(E2EHost host) : BrowserTestBase(host)
{
    private const string SearchesPath = "/api/v1/homefinder/searches";
    private const string SearchName = "E2E weekend search";

    private const string SavedSearchesFocus = "saved-searches-focus";

    // App behaviour that is wrong today, so this suite can land before its fix (a separate pull request). Like the axe baseline
    // and the layout suite's KnownOverflows it only shrinks: while an entry is listed the test asserts the behaviour as it is, and
    // the day the app is fixed that assertion fails and says to delete the entry, after which the test asserts the right behaviour.
    private static readonly Dictionary<string, string> KnownIssues = new()
    {
        [SavedSearchesFocus] =
            "Escape on the Saved Searches dialog leaves focus on <body> instead of returning it to #btnLoadSearches. Bootstrap only " +
            "restores focus to data-api triggers, and HomeFinder/app.js opens this dialog from script (a new bootstrap.Modal on each " +
            "click); only the Save Search dialog moves focus back itself (saved-searches.js, in its hidden handler)."
    };

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
        await AssertFocusReturnedAsync(SavedSearchesFocus, "#btnLoadSearches");

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

    // Bootstrap removes `show` when the hide transition has finished, then the page moves focus back in its hidden handler.
    private Task WaitForDialogToCloseAsync(string id) =>
        Page.WaitForFunctionAsync("id => !document.getElementById(id).classList.contains('show') && document.getElementById(id).style.display === 'none'", id);

    // Focus is back on the button that opened the dialog, unless the app has a known issue with that (see KnownIssues): then the
    // test pins what happens today (focus dropped to <body>), so fixing the app fails it until the entry is deleted.
    private async Task AssertFocusReturnedAsync(string issue, string buttonSelector)
    {
        // The selector itself when the button has focus, otherwise the tag name of whatever has.
        var active = await Page.EvaluateAsync<string>(
            "selector => document.activeElement === document.querySelector(selector) ? selector : document.activeElement.tagName",
            buttonSelector);
        var returned = active == buttonSelector;

        if (KnownIssues.TryGetValue(issue, out var reason))
        {
            Assert.False(
                returned,
                $"Focus now returns to {buttonSelector}, so the known issue '{issue}' is fixed: delete its entry from KnownIssues (it only shrinks). The entry said: {reason}");
            Assert.True(active == "BODY", $"Known issue '{issue}': focus was expected to be dropped to BODY, but it is on {active}. {reason}");
        }
        else
        {
            Assert.True(returned, $"Escape should return focus to {buttonSelector}, but it is on {active}.");
        }
    }

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
