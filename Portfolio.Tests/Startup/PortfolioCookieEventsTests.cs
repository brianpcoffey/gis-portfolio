using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Startup;

namespace Portfolio.Tests.Startup;

public class PortfolioCookieEventsTests
{
    private static readonly AuthenticationScheme Scheme =
        new(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));

    private static DefaultHttpContext Http(string path, IServiceProvider? services = null)
    {
        var ctx = new DefaultHttpContext { RequestServices = services ?? new ServiceCollection().BuildServiceProvider() };
        ctx.Request.Path = path;
        return ctx;
    }

    private static RedirectContext<CookieAuthenticationOptions> Redirect(HttpContext http, string uri) =>
        new(http, Scheme, new CookieAuthenticationOptions(), new AuthenticationProperties(), uri);

    private static ClaimsPrincipal GooglePrincipal(string? googleId = "g-123")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, "a@example.test"),
            new(ClaimTypes.Name, "Ada"),
            new("picture", "https://example.test/p.png")
        };
        if (googleId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, googleId));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Google"));
    }

    private static CookieSignedInContext SignedIn(HttpContext http, ClaimsPrincipal principal) =>
        new(http, Scheme, principal, new AuthenticationProperties(), new CookieAuthenticationOptions());

    private static (DefaultHttpContext Http, Mock<IUserProfileService> Service) HttpWithProfileService(Guid userId, Action<GoogleProfileDto>? onCall = null)
    {
        var service = new Mock<IUserProfileService>();
        service.Setup(s => s.CreateOrUpdateFromGoogleAsync(It.IsAny<GoogleProfileDto>(), It.IsAny<CancellationToken>()))
            .Callback<GoogleProfileDto, CancellationToken>((dto, _) => onCall?.Invoke(dto))
            .ReturnsAsync(userId);
        var provider = new ServiceCollection().AddSingleton(service.Object).BuildServiceProvider();
        return (Http("/Login", provider), service);
    }

    // ── Redirects ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RedirectToLogin_ForApiPath_Returns401WithoutLocation()
    {
        // Arrange
        var http = Http("/api/v1/fiber/orders");

        // Act
        await new PortfolioCookieEvents().RedirectToLogin(Redirect(http, "/Login?ReturnUrl=x"));

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, http.Response.StatusCode);
        Assert.False(http.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task RedirectToAccessDenied_ForApiPath_Returns403WithoutLocation()
    {
        // Arrange
        var http = Http("/api/v1/fiber/orders");

        // Act
        await new PortfolioCookieEvents().RedirectToAccessDenied(Redirect(http, "/AccessDenied"));

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, http.Response.StatusCode);
        Assert.False(http.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task RedirectToLogin_ForPage_Redirects302ToLoginUri()
    {
        // Arrange
        var http = Http("/Account/Profile");

        // Act
        await new PortfolioCookieEvents().RedirectToLogin(Redirect(http, "/Login?ReturnUrl=%2FAccount%2FProfile"));

        // Assert
        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
        Assert.Equal("/Login?ReturnUrl=%2FAccount%2FProfile", http.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task RedirectToAccessDenied_ForPage_Redirects302()
    {
        // Arrange
        var http = Http("/Account/Profile");

        // Act
        await new PortfolioCookieEvents().RedirectToAccessDenied(Redirect(http, "/AccessDenied"));

        // Assert
        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
        Assert.Equal("/AccessDenied", http.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task ApiAwareRedirect_ApiPathIsMatchedBySegment_NotBySubstring()
    {
        // Arrange: "/apiary" is not under "/api".
        var http = Http("/apiary");

        // Act
        await PortfolioCookieEvents.ApiAwareRedirect(Redirect(http, "/Login"), StatusCodes.Status401Unauthorized);

        // Assert
        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
    }

    // ── SignedIn ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SignedIn_WithAnonymousCookie_MergesDeletesCookieAndSetsIdentity()
    {
        // Arrange
        var userId = Guid.NewGuid();
        GoogleProfileDto? seen = null;
        var (http, service) = HttpWithProfileService(userId, dto => seen = dto);
        http.Request.Headers.Cookie = $"AnonUserId={Guid.NewGuid()}";

        // Act
        await new PortfolioCookieEvents().SignedIn(SignedIn(http, GooglePrincipal()));

        // Assert
        service.Verify(s => s.CreateOrUpdateFromGoogleAsync(It.IsAny<GoogleProfileDto>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(seen);
        Assert.Equal("g-123", seen!.GoogleId);
        Assert.Equal("a@example.test", seen.Email);
        Assert.Equal("Ada", seen.Name);
        Assert.Equal("https://example.test/p.png", seen.Picture);
        Assert.Contains(http.Response.Headers.SetCookie.ToArray(),
            c => c!.StartsWith("AnonUserId=;", StringComparison.Ordinal));
        Assert.Equal(userId, http.Items["PortfolioIdentity"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task SignedIn_WithoutUsableAnonymousCookie_DoesNotDeleteCookieButSetsIdentity(string? cookie)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (http, _) = HttpWithProfileService(userId);
        if (cookie is not null)
            http.Request.Headers.Cookie = $"AnonUserId={cookie}";

        // Act
        await new PortfolioCookieEvents().SignedIn(SignedIn(http, GooglePrincipal()));

        // Assert
        Assert.Equal(0, http.Response.Headers.SetCookie.Count);
        Assert.Equal(userId, http.Items["PortfolioIdentity"]);
    }

    [Fact]
    public async Task SignedIn_WhenPrincipalHasNoGoogleId_DoesNothing()
    {
        // Arrange
        var (http, service) = HttpWithProfileService(Guid.NewGuid());

        // Act
        await new PortfolioCookieEvents().SignedIn(SignedIn(http, GooglePrincipal(googleId: null)));

        // Assert
        service.Verify(s => s.CreateOrUpdateFromGoogleAsync(It.IsAny<GoogleProfileDto>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(http.Items.ContainsKey("PortfolioIdentity"));
    }

    [Fact]
    public async Task SignedIn_WhenPrincipalIsNotAuthenticated_DoesNothing()
    {
        // Arrange
        var (http, service) = HttpWithProfileService(Guid.NewGuid());
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        await new PortfolioCookieEvents().SignedIn(SignedIn(http, anonymous));

        // Assert
        service.Verify(s => s.CreateOrUpdateFromGoogleAsync(It.IsAny<GoogleProfileDto>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(http.Items.ContainsKey("PortfolioIdentity"));
    }
}
