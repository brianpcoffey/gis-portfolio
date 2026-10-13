using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Repositories;

namespace Portfolio.Tests.Integration;

/// <summary>
/// Characterizes the middleware order documented in docs/agent-context.md section 4 by observing
/// it from outside: forwarded headers, security headers, static files, rate limiter, exception
/// middleware, anonymous identity, authentication.
/// </summary>
/// <remarks>Every assertion holds on the pipeline built by UsePortfolioPipeline (Startup/Pipeline.cs).</remarks>
public class PipelineOrderTests
{
    private const string CookieName = "AnonUserId";

    private static void AssertSecurityHeaders(HttpResponseMessage r)
    {
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", r.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("frame-ancestors 'none'; base-uri 'self'; object-src 'none'",
            r.Headers.GetValues("Content-Security-Policy").Single());
    }

    private static HttpRequestMessage Get(string path, string? accept = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        if (accept is not null) req.Headers.Accept.ParseAdd(accept);
        return req;
    }

    private static bool HasAnonCookie(HttpResponseMessage r) =>
        r.Headers.TryGetValues("Set-Cookie", out var cookies) &&
        cookies.Any(c => c.StartsWith(CookieName + "=", StringComparison.Ordinal));

    [Fact]
    public async Task HealthLive_Returns200_WithSecurityHeaders()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var r = await f.Client().GetAsync("/health/live", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        AssertSecurityHeaders(r);
    }

    [Fact]
    public async Task StaticFile_IsServedBeforeRouting_WithSecurityHeaders()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var r = await f.Client().GetAsync("/js/api-config.js", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        AssertSecurityHeaders(r);
    }

    [Fact]
    public async Task ProtectedApi_WithoutAuth_Is401_WithoutRedirect()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var r = await f.Client().GetAsync("/api/v1/fiber/orders", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Null(r.Headers.Location);
    }

    [Fact]
    public async Task ProtectedPage_WithoutAuth_RedirectsToAppLogin()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var r = await f.Client().GetAsync("/Account/Profile", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.NotNull(r.Headers.Location);
        Assert.Equal("/Login", r.Headers.Location!.AbsolutePath);
        Assert.Contains("ReturnUrl=", r.Headers.Location.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GlobalLimiter_301stRequest_Is429_BeforeAnonymousIdentity_AndPartitionedByForwardedIp()
    {
        // Arrange
        // An unrouted path never reaches MVC, so 300 requests are cheap.
        await using var f = new PortfolioAppFactory();
        var c = f.Client();
        for (var i = 0; i < 300; i++)
        {
            var ok = await c.GetAsync("/api/v1/no-such-route", TestContext.Current.CancellationToken);
            Assert.True(ok.StatusCode == HttpStatusCode.NotFound, $"#{i}: {(int)ok.StatusCode}");
        }

        // Act & Assert
        var r = await c.SendAsync(Get("/api/v1/no-such-route", "text/html"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, r.StatusCode);
        AssertSecurityHeaders(r);
        Assert.False(HasAnonCookie(r), "a rate-limited request must be rejected before AnonymousUserMiddleware issues a cookie");
        Assert.Equal(0, f.ProfileCount());

        // ForwardedHeaders runs first, so a different X-Forwarded-For is a fresh partition.
        var other = Get("/api/v1/no-such-route");
        other.Headers.Add("X-Forwarded-For", "198.51.100.7");
        var r2 = await c.SendAsync(other, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, r2.StatusCode);

        // Health probes opt out of rate limiting.
        var health = await c.GetAsync("/health/live", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task HtmlRequest_WithoutCookie_CreatesProfile_AndHttpOnlyLaxCookie()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var r = await f.Client().SendAsync(Get("/Privacy", "text/html"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var cookie = r.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith(CookieName + "=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, f.ProfileCount());
    }

    [Fact]
    public async Task JsonRequest_WithoutCookie_CreatesNoProfile_AndApiExceptionMiddlewareMapsTheConflict()
    {
        // Arrange
        // With no cookie and no text/html Accept, AnonymousUserMiddleware passes through without an
        // identity. SavedFeatures then throws InvalidOperationException from GetCurrentUserId, and
        // only ApiExceptionMiddleware being in the pipeline turns that into a 409 ProblemDetails.
        await using var f = new PortfolioAppFactory();

        // Act
        var r = await f.Client().SendAsync(Get("/api/v1/features/saved", "application/json"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType);
        var body = await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"title\":\"Conflict\"", body, StringComparison.Ordinal);
        Assert.False(HasAnonCookie(r));
        Assert.Equal(0, f.ProfileCount());
    }
}
