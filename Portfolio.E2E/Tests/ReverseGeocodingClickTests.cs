using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Playwright;
using Portfolio.Common.DTOs;
using Portfolio.Common.Serialization;
using Portfolio.E2E.Infrastructure;

namespace Portfolio.E2E.Tests;

/// <summary>
/// Clicking the Reverse Geocoding map looks the clicked point up and shows the answer in the result card, every time, even when
/// the answer arrives at once. The lookup is answered in the browser from a canned DTO, so nothing reaches the server's ArcGIS
/// client, let alone ArcGIS geocoding (which no test may reach).
/// </summary>
/// <remarks>
/// An instant answer finishes inside the same turn of the page's event loop that handled the click, which is the case that
/// can race the map view's own click handling. The marker popup is not asserted here: it is a separate behaviour, and it does
/// not open at all on ArcGIS 4.29 today.
/// </remarks>
public sealed class ReverseGeocodingClickTests(E2EHost host) : BrowserTestBase(host)
{
    private const string ReversePath = "/api/v1/geocoding/reverse";

    // The one uncaught page error this scenario tolerates, and only here: from the second map click on, the page reports an
    // unhandled promise rejection, "AbortError" with the reason "Aborted" and no stack (observed on ArcGIS 4.29 with answers
    // that arrive at once and with answers 2 s apart, in most runs, with no console error). It does not stop the lookups or
    // the card. It is a finding for a follow-up, not an expectation: when it stops happening this test still passes.
    private const string KnownPageError = "AbortError";

    // The centre, then three points below it. Clicks on the same spot in quick succession read as a double click (zoom), so each
    // later click is well away from the one before; every click still gets its own coordinates and its own answer. Each point is
    // below the one before because a click's popup opens above its marker (docked in the top-right corner when there is no
    // room): a click that lands on an open popup is a click on the popup's own buttons, not on the map.
    private static readonly (int X, int Y)[] ClickOffsets = [(0, 0), (80, 50), (-80, 100), (0, 150)];

    [E2EFact]
    public async Task MapClick_AnsweredInstantly_UpdatesTheResultCardOnEveryClick()
    {
        // Arrange
        await StartAsync();
        var lookups = new ConcurrentQueue<(double Lat, double Lng)>();
        await Page.RouteAsync($"**{ReversePath}**", route =>
        {
            var query = System.Web.HttpUtility.ParseQueryString(new Uri(route.Request.Url).Query);
            var lat = double.Parse(query["lat"]!, CultureInfo.InvariantCulture);
            var lng = double.Parse(query["lng"]!, CultureInfo.InvariantCulture);
            lookups.Enqueue((lat, lng));

            // No delay: the response is fulfilled in the handler itself.
            return route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(Answer(lat, lng), PortfolioJsonOptions.Default)
            });
        });
        await Loader.LoadAsync(PageCatalog.Find("/Projects/ReverseGeocoding"));
        await Page.Locator("#rgMapView").ScrollIntoViewIfNeededAsync();
        var map = await Page.Locator("#rgMapView").BoundingBoxAsync();
        Assert.NotNull(map);
        var card = Page.Locator("#rgResultCard");
        var address = Page.Locator("#rgMatchAddress");

        for (var click = 0; click < ClickOffsets.Length; click++)
        {
            // Act
            var (dx, dy) = ClickOffsets[click];
            await Page.Mouse.ClickAsync(map.X + map.Width / 2 + dx, map.Y + map.Height / 2 + dy);

            // Assert: this click was looked up, and the card shows the answer to it (not the one before).
            Assert.True(await WaitForLookupAsync(lookups, click + 1), $"click {click + 1}: the page never asked for a reverse lookup.");
            var asked = lookups.ElementAt(click);
            await Assertions.Expect(card).ToBeVisibleAsync();
            await Assertions.Expect(address).ToHaveTextAsync(AddressFor(asked.Lat, asked.Lng));
            await Assertions.Expect(Page.Locator("#rgSpinner")).ToBeHiddenAsync();
        }

        Assert.Equal(ClickOffsets.Length, lookups.Count);
        Assert.Empty(Session.UnexpectedConsoleErrors(ConsoleAllowList.ForPage(await Page.EvaluateAsync<string?>("() => window.PortfolioEsri?.baseUrl ?? null"))));
        // Every other page error fails the test; the one tolerated here is described below. Requests to hosts the policy does
        // not know are checked when the session is disposed.
        Assert.All(Session.PageErrors, error => Assert.StartsWith(KnownPageError, error, StringComparison.Ordinal));
    }

    // A different answer for every point, so a card that was not updated cannot pass for one that was.
    private static ReverseGeocodingResultDto Answer(double lat, double lng) => new()
    {
        Latitude = lat,
        Longitude = lng,
        MatchedAddress = AddressFor(lat, lng),
        HouseNumber = "1",
        Street = "Canned Street",
        City = "Redlands",
        Region = "CA",
        PostalCode = "92373",
        CountryCode = "USA",
        LocationType = "StreetAddress"
    };

    private static string AddressFor(double lat, double lng) =>
        string.Create(CultureInfo.InvariantCulture, $"1 Canned Street ({lat:0.0000}, {lng:0.0000})");

    // The route handler runs on Playwright's event thread: wait (on a condition) until it has seen the nth lookup.
    private static async Task<bool> WaitForLookupAsync(ConcurrentQueue<(double Lat, double Lng)> lookups, int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (lookups.Count < count && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        return lookups.Count >= count;
    }
}
