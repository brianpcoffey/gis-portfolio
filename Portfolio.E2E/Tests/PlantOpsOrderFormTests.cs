using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Portfolio.E2E.Infrastructure;

namespace Portfolio.E2E.Tests;

/// <summary>
/// The Plant Operations order form saves once however fast it is clicked: a double click makes exactly one
/// <c>POST /api/v1/fiber/orders</c>, adds exactly one row to the table, and the new order gets a well-formed number.
/// (Two overlapping creates could otherwise be issued the same order number.)
/// </summary>
public sealed class PlantOpsOrderFormTests(E2EHost host) : BrowserTestBase(host)
{
    private const string OrdersPath = "/api/v1/fiber/orders";
    private const string ClientName = "E2E Double Click Client";

    // The DataTable behind the orders tab, counted through its API (the DOM shows only one page of rows).
    private const string OrderCount = "() => $('#plantOpsOrdersTable').DataTable().rows().count()";

    [E2EFact]
    public async Task OrderForm_SubmitDoubleClicked_PostsOnceAndAddsOneRow()
    {
        // Arrange
        await StartAsync();
        var posts = new ConcurrentQueue<IRequest>();
        Page.Request += (_, request) =>
        {
            if (request.Method == "POST" && new Uri(request.Url).AbsolutePath == OrdersPath)
                posts.Enqueue(request);
        };
        await Loader.LoadAsync(PageCatalog.Find("/Projects/PlantOperationsDashboard"));
        // The orders arrive after the page's start-up requests; the seed gives a new visitor a set of demo orders.
        await Page.WaitForFunctionAsync("() => typeof $ === 'function' && $.fn.dataTable !== undefined && $.fn.dataTable.isDataTable('#plantOpsOrdersTable')");
        var before = await Page.EvaluateAsync<int>(OrderCount);
        Assert.True(before > 0, "the sign-in seed should have given the visitor demo orders");

        await Page.Locator("#btnNewOrder").ClickAsync();
        var form = Page.Locator("#orderForm");
        await form.WaitForAsync();
        await form.Locator("input[name=clientName]").FillAsync(ClientName);
        await form.Locator("input[name=productName]").FillAsync("Fiber spool, 12-strand");
        await form.Locator("input[name=quantity]").FillAsync("5");
        await form.Locator("input[name=unitPrice]").FillAsync("12.50");
        await form.Locator("input[name=orderDate]").FillAsync("2026-10-12");
        await form.Locator("input[name=shipDate]").FillAsync("2026-10-19");

        // Act: a window-level capture listener sees both clicks of the double click before anything on the page can stop one,
        // which shows the test really delivers two clicks to the button.
        await Page.EvaluateAsync(
            "() => { window.submitClicks = 0; window.addEventListener('click', e => { if (e.target.closest && e.target.closest('#orderForm button[type=submit]')) window.submitClicks++; }, true); }");
        await form.Locator("button[type=submit]").DblClickAsync();
        // The table is reloaded after the save, so a row more is the end of the flow.
        await Page.WaitForFunctionAsync($"n => ({OrderCount})() === n + 1", before);
        // The table shows ten rows a page, so the new one is found through the table's own search box.
        await Page.Locator("#plantOpsOrdersTable_filter input").FillAsync(ClientName);
        var created = Page.Locator("#plantOpsOrdersTable tbody tr", new PageLocatorOptions { HasText = ClientName });
        var orderNumber = (await created.First.Locator("td").First.InnerTextAsync()).Trim();

        // Assert: counted after the table has changed, which needs the first response, so a second POST (issued within
        // milliseconds of the first, before that response) would be queued by now.
        Assert.Equal(2, await Page.EvaluateAsync<int>("() => window.submitClicks"));
        var post = Assert.Single(posts);
        var response = await post.ResponseAsync();
        Assert.NotNull(response);
        Assert.Equal(201, response.Status);
        Assert.Equal(before + 1, await Page.EvaluateAsync<int>(OrderCount));
        Assert.Equal(1, await created.CountAsync());
        Assert.Matches(new Regex(@"^ORD-\d{8}-\d{4}$", RegexOptions.None, TimeSpan.FromSeconds(1)), orderNumber);
        Session.AssertClean(ConsoleAllowList.ForPage(await Page.EvaluateAsync<string?>("() => window.PortfolioEsri?.baseUrl ?? null")));
    }
}
