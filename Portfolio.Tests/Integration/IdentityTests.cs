using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.Constants;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories;
using Portfolio.Repositories.Repositories;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Integration;

/// <summary>
/// Identity separation (WP-15): the anonymous GUID cookie identity must never be able to act as, or
/// take over, a Google-linked profile. Host tests use <see cref="PortfolioAppFactory"/>; the sign-in
/// merge is exercised on the real service and repository over EF InMemory (never real Google).
/// </summary>
public class IdentityTests
{
    private const string CookieName = "AnonUserId";

    private static HttpRequestMessage Get(string path, string? accept = null, string? cookie = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        if (accept is not null) req.Headers.Accept.ParseAdd(accept);
        if (cookie is not null) req.Headers.TryAddWithoutValidation("Cookie", $"{CookieName}={cookie}");
        return req;
    }

    private static string? SetCookieFor(HttpResponseMessage r) =>
        r.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(c => c.StartsWith(CookieName + "=", StringComparison.Ordinal))
            : null;

    // A client that does not persist cookies, so each request is a fresh visitor.
    private static HttpClient Bare(PortfolioAppFactory f) =>
        f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

    private static async Task<T> WithDb<T>(PortfolioAppFactory f, Func<PortfolioDbContext, Task<T>> work)
    {
        using var scope = f.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<PortfolioDbContext>());
    }

    // ── Host: what a visit creates ───────────────────────────────────────────

    [Fact]
    public async Task FreshAnonymousVisit_Today_SeedsRows()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var r = await Bare(f).SendAsync(Get("/Privacy", "text/html"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(1, f.ProfileCount());
        Assert.True(await WithDb(f, db => db.FiberClients.CountAsync(TestContext.Current.CancellationToken)) > 0);
    }

    [Fact]
    public async Task ApiRequest_WithHtmlAccept_AndNoCookie_Today_CreatesAProfile()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        await Bare(f).SendAsync(Get("/api/v1/no-such-route", "text/html"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, f.ProfileCount());
    }

    [Fact]
    public async Task EleventhNewIdentity_Today_StillGetsACookie()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var c = Bare(f);

        // Act
        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
            last = await c.SendAsync(Get("/Privacy", "text/html"), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(SetCookieFor(last!));
        Assert.Equal(11, f.ProfileCount());
    }

    // ── Service: sign-in merge over a real repository ────────────────────────

    private sealed class SignInRig : IDisposable
    {
        private readonly DbContextOptions<PortfolioDbContext> _options;
        private readonly PortfolioDbContext _db;
        public readonly DefaultHttpContext Http = new();
        public readonly UserProfileService Service;

        public SignInRig()
        {
            _options = new DbContextOptionsBuilder<PortfolioDbContext>()
                .UseInMemoryDatabase($"Identity_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _db = new PortfolioDbContext(_options);
            Service = new UserProfileService(
                new HttpContextAccessor { HttpContext = Http },
                new UserProfileRepository(_db, NullLogger<UserProfileRepository>.Instance),
                TimeProvider.System,
                new UserProfileSeedService(_db, NullLogger<UserProfileSeedService>.Instance),
                NullLogger<UserProfileService>.Instance);
        }

        public PortfolioDbContext Fresh() => new(_options);

        public async Task<Guid> AddProfile(string? googleId)
        {
            var id = Guid.NewGuid();
            _db.UserProfiles.Add(new UserProfile { UserId = id, CreatedDate = DateTime.UtcNow, LastActiveDate = DateTime.UtcNow });
            if (googleId is not null)
                _db.UserClaims.Add(new UserClaim { UserId = id, ClaimType = ProfileClaimTypes.GoogleId, ClaimValue = googleId });
            await _db.SaveChangesAsync();
            return id;
        }

        public void CarryCookie(Guid id) => Http.Request.Headers.Cookie = $"{CookieName}={id}";

        public void Dispose() => _db.Dispose();
    }

    private static GoogleProfileDto Google(string id) => new() { GoogleId = id, Email = $"{id}@example.test", Name = id };

    [Fact]
    public async Task SignIn_WithCookieOfALinkedProfile_Today_AdoptsItAndOverwritesTheVictimsGoogleId()
    {
        // Arrange
        // Characterisation of the takeover WP-15 closes: the attacker's sign-in re-points the
        // victim's profile at the attacker's Google account.
        using var rig = new SignInRig();
        var victim = await rig.AddProfile("g-victim");
        rig.CarryCookie(victim);

        // Act
        var result = await rig.Service.CreateOrUpdateFromGoogleAsync(Google("g-attacker"));

        // Assert
        Assert.Equal(victim, result);
        await using var db = rig.Fresh();
        var claim = await db.UserClaims.SingleAsync(c => c.UserId == victim && c.ClaimType == ProfileClaimTypes.GoogleId);
        Assert.Equal("g-attacker", claim.ClaimValue);
    }

    [Fact]
    public async Task SignIn_WithCookieOfAnUnlinkedProfile_Today_AdoptsItWithoutSeeding()
    {
        // Arrange
        using var rig = new SignInRig();
        var anon = await rig.AddProfile(null);
        rig.CarryCookie(anon);

        // Act
        var result = await rig.Service.CreateOrUpdateFromGoogleAsync(Google("g-new"));

        // Assert
        Assert.Equal(anon, result);
        await using var db = rig.Fresh();
        Assert.Empty(db.FiberClients);
        Assert.Equal("g-new", (await db.UserClaims.SingleAsync(c => c.UserId == anon && c.ClaimType == ProfileClaimTypes.GoogleId)).ClaimValue);
    }
}
