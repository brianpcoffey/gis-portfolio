using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Common.Models;
using Portfolio.Repositories;

namespace Portfolio.Tests.Integration;

/// <summary>
/// EN-06: the second CSRF layer through the real pipeline: a cookie-bearing, state-changing /api request needs
/// X-Requested-With; everything else is unchanged.
/// </summary>
/// <remarks>
/// One factory per test (rate-limit state lives in the host). The clients keep no cookies, so each request carries
/// exactly the Cookie header the test writes.
/// </remarks>
public class ApiRequestHeaderTests
{
    private const string AnonCookieName = "AnonUserId";
    private const string RejectionBody = "{\"error\":\"This request must include the X-Requested-With header.\"}";
    private const string SavedFeatures = "/api/v1/features/saved";

    private static readonly object NewFeature = new
    {
        layerId = "3",
        featureId = "1",
        name = "Nevada",
        geometryJson = "{\"type\":\"Point\",\"coordinates\":[-117,36]}"
    };

    private static HttpClient NewClient(PortfolioAppFactory f) =>
        f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private static HttpRequestMessage Request(HttpMethod method, string path, string? cookie = null, bool withHeader = false, object? json = null)
    {
        var req = new HttpRequestMessage(method, path);
        if (json is not null) req.Content = JsonContent.Create(json);
        if (cookie is not null) req.Headers.TryAddWithoutValidation("Cookie", cookie);
        if (withHeader) req.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        return req;
    }

    private static async Task<DateTime> AddProfile(PortfolioAppFactory f, Guid id, DateTime lastActive)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        db.UserProfiles.Add(new UserProfile { UserId = id, CreatedDate = lastActive, LastActiveDate = lastActive });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return lastActive;
    }

    private static DateTime LastActive(PortfolioAppFactory f, Guid id)
    {
        using var scope = f.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PortfolioDbContext>().UserProfiles.Single(p => p.UserId == id).LastActiveDate;
    }

    private static void AssertRejected(HttpResponseMessage r, string body)
    {
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Equal("application/json", r.Content.Headers.ContentType?.MediaType);
        Assert.Equal(RejectionBody, body);
    }

    [Fact]
    public async Task Post_WithAnonymousCookieAndNoHeader_Returns403_WithSecurityHeaders_NoStore_AndLeavesTheProfileUntouched()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var user = Guid.NewGuid();
        var stale = await AddProfile(f, user, DateTime.UtcNow.AddDays(-1));

        // Act
        var r = await NewClient(f).SendAsync(
            Request(HttpMethod.Post, SavedFeatures, $"{AnonCookieName}={user}", json: NewFeature),
            TestContext.Current.CancellationToken);
        var body = await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        AssertRejected(r, body);
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("frame-ancestors 'none'; base-uri 'self'; object-src 'none'", r.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("no-store", r.Headers.CacheControl?.ToString());
        Assert.False(r.Headers.Contains("Set-Cookie"), "a rejected request must not issue a cookie");
        Assert.Equal(stale, LastActive(f, user));
        Assert.Equal(1, f.ProfileCount());
    }

    [Fact]
    public async Task Post_WithAnonymousCookieAndHeader_BehavesAsBefore_SavesTheFeatureAndTouchesTheProfile()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var user = Guid.NewGuid();
        var stale = await AddProfile(f, user, DateTime.UtcNow.AddDays(-1));

        // Act
        var r = await NewClient(f).SendAsync(
            Request(HttpMethod.Post, SavedFeatures, $"{AnonCookieName}={user}", withHeader: true, json: NewFeature),
            TestContext.Current.CancellationToken);
        var saved = await r.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("Nevada", saved.GetProperty("name").GetString());
        Assert.True(LastActive(f, user) > stale, "the anonymous-identity middleware should have run");
    }

    [Theory]
    [InlineData("POST", "")]
    [InlineData("PUT", "/1")]
    [InlineData("PATCH", "/1")]
    [InlineData("DELETE", "/1")]
    public async Task UnsafeMethods_WithAnonymousCookieAndNoHeader_AreRejected_BeforeRouting(string method, string suffix)
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var user = Guid.NewGuid();
        await AddProfile(f, user, DateTime.UtcNow);

        // Act
        var r = await NewClient(f).SendAsync(
            Request(new HttpMethod(method), SavedFeatures + suffix, $"{AnonCookieName}={user}"),
            TestContext.Current.CancellationToken);
        var body = await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        AssertRejected(r, body);
    }

    [Fact]
    public async Task Delete_WithAnonymousCookieAndHeader_ReachesTheController()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var user = Guid.NewGuid();
        await AddProfile(f, user, DateTime.UtcNow);

        // Act
        var r = await NewClient(f).SendAsync(
            Request(HttpMethod.Delete, SavedFeatures + "/12345", $"{AnonCookieName}={user}", withHeader: true),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task Post_WithTheCookieTheAppIssued_AndNoHeader_Returns403()
    {
        // Arrange
        // Takes the cookie name and value from a real Set-Cookie, so the middleware's cookie name cannot drift
        // from the one AnonymousUserMiddleware issues.
        await using var f = new PortfolioAppFactory();
        var client = NewClient(f);
        var page = Request(HttpMethod.Get, "/Privacy");
        page.Headers.Accept.ParseAdd("text/html");
        var visit = await client.SendAsync(page, TestContext.Current.CancellationToken);
        var issued = visit.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(AnonCookieName + "=", StringComparison.Ordinal));
        var cookie = issued[..issued.IndexOf(';')];

        // Act
        var without = await client.SendAsync(Request(HttpMethod.Post, SavedFeatures, cookie, json: NewFeature), TestContext.Current.CancellationToken);
        var with = await client.SendAsync(Request(HttpMethod.Post, SavedFeatures, cookie, withHeader: true, json: NewFeature), TestContext.Current.CancellationToken);

        // Assert
        AssertRejected(without, await without.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, with.StatusCode);
    }

    [Theory]
    [InlineData(".AspNetCore.Cookies")]
    [InlineData(".AspNetCore.CookiesC1")]
    public async Task Post_WithTheAuthCookieOrAChunk_AndNoHeader_Returns403_AndWithTheHeaderIsNot(string cookieName)
    {
        // Arrange
        // The value is not a valid ticket; the rule keys on the cookie's presence, before authentication runs.
        await using var f = new PortfolioAppFactory();
        var client = NewClient(f);

        // Act
        var without = await client.SendAsync(Request(HttpMethod.Post, SavedFeatures, $"{cookieName}=not-a-ticket", json: NewFeature), TestContext.Current.CancellationToken);
        var with = await client.SendAsync(Request(HttpMethod.Post, SavedFeatures, $"{cookieName}=not-a-ticket", withHeader: true, json: NewFeature), TestContext.Current.CancellationToken);

        // Assert
        AssertRejected(without, await without.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.NotEqual(HttpStatusCode.Forbidden, with.StatusCode);
    }

    [Fact]
    public async Task Get_WithAnonymousCookieAndNoHeader_IsNotRejected()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var user = Guid.NewGuid();
        await AddProfile(f, user, DateTime.UtcNow);

        // Act
        var r = await NewClient(f).SendAsync(Request(HttpMethod.Get, SavedFeatures, $"{AnonCookieName}={user}"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task CspReport_WithAnonymousCookieAndNoHeader_Returns204()
    {
        // Arrange
        // Browsers send CSP reports themselves, with the page's cookies and no way to add a header.
        await using var f = new PortfolioAppFactory();
        using var content = new StringContent("{}", Encoding.UTF8);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/csp-report");
        using var req = new HttpRequestMessage(HttpMethod.Post, "/csp-report") { Content = content };
        req.Headers.TryAddWithoutValidation("Cookie", $"{AnonCookieName}={Guid.NewGuid()}; .AspNetCore.Cookies=not-a-ticket");

        // Act
        var r = await NewClient(f).SendAsync(req, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
    }

    [Fact]
    public async Task ComputePost_WithNoCookieAndNoHeader_Returns200()
    {
        // Arrange
        // No cookie means no ambient authority: curl, probes and Scalar without cookies pass unchanged.
        await using var f = new PortfolioAppFactory();
        var points = new { points = new[] { new { x = 0, y = 0 }, new { x = 1, y = 0 }, new { x = 0, y = 1 } } };

        // Act
        var r = await NewClient(f).SendAsync(Request(HttpMethod.Post, "/api/v1/geometry/triangulate", json: points), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Post_WithOnlyAnUnrelatedCookieAndNoHeader_IsNotRejected()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var points = new { points = new[] { new { x = 0, y = 0 }, new { x = 1, y = 0 }, new { x = 0, y = 1 } } };

        // Act
        var r = await NewClient(f).SendAsync(
            Request(HttpMethod.Post, "/api/v1/geometry/triangulate", ".AspNetCore.Session=abc; theme=dark", json: points),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task ScalarReference_LoadsTheConfigModule_ThatSetsTheHeader()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var client = NewClient(f);

        // Act
        var page = await client.GetAsync("/scalar/v1", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var module = await client.GetAsync("/js/scalar-config.js", TestContext.Current.CancellationToken);
        var script = await module.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("/js/scalar-config.js", html, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, module.StatusCode);
        Assert.Contains("onBeforeRequest", script, StringComparison.Ordinal);
        Assert.Contains("X-Requested-With", script, StringComparison.Ordinal);
    }
}
