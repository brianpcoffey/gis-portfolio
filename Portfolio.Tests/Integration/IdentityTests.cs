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
    public async Task FreshAnonymousVisit_InsertsOneProfileAndNoSeedRows()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var r = await Bare(f).SendAsync(Get("/Privacy", "text/html"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(1, f.ProfileCount());
        // Flipped (WP-15, D-05): the characterisation asserted seeded fiber clients here.
        Assert.Equal(0, await WithDb(f, db => db.FiberClients.CountAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(0, await WithDb(f, db => db.FiberMaterials.CountAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(0, await WithDb(f, db => db.Properties.CountAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task ApiRequest_WithHtmlAccept_AndNoCookie_CreatesNoProfile()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var r = await Bare(f).SendAsync(Get("/api/v1/no-such-route", "text/html"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, f.ProfileCount());
        Assert.Null(SetCookieFor(r));
    }

    [Fact]
    public async Task CookieOfALinkedProfile_GetsNoIdentity_AndTheCookieIsDeleted()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var victim = Guid.NewGuid();
        await WithDb(f, async db =>
        {
            db.UserProfiles.Add(new UserProfile { UserId = victim, CreatedDate = DateTime.UtcNow, LastActiveDate = DateTime.UtcNow });
            db.UserClaims.Add(new UserClaim { UserId = victim, ClaimType = ProfileClaimTypes.GoogleId, ClaimValue = "g-victim" });
            await db.SaveChangesAsync();
            return 0;
        });

        // Act
        var r = await Bare(f).SendAsync(Get("/api/v1/features/saved", "application/json", victim.ToString()), TestContext.Current.CancellationToken);

        // Assert
        // No identity, so the saved-features endpoint cannot act as the victim (409, as for any
        // request without an identity).
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        var deleted = SetCookieFor(r);
        Assert.NotNull(deleted);
        Assert.Contains("expires=", deleted, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, f.ProfileCount());
    }

    [Fact]
    public async Task EleventhNewIdentity_FromOneAddress_GetsNoCookie()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var c = Bare(f);

        // Act
        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
            last = await c.SendAsync(Get("/Privacy", "text/html"), TestContext.Current.CancellationToken);

        // Assert
        // Flipped (WP-15): the characterisation expected a cookie for the 11th as well.
        Assert.Equal(HttpStatusCode.OK, last!.StatusCode);
        Assert.Null(SetCookieFor(last));
        Assert.Equal(10, f.ProfileCount());
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
    public async Task SignIn_WithCookieOfSomeoneElsesLinkedProfile_CreatesANewProfile_AndLeavesTheVictimUntouched()
    {
        // Arrange
        // Flipped from the WP-15 characterisation, which adopted the victim's profile and
        // re-pointed its google_id at the attacker.
        using var rig = new SignInRig();
        var victim = await rig.AddProfile("g-victim");
        rig.CarryCookie(victim);

        // Act
        var result = await rig.Service.CreateOrUpdateFromGoogleAsync(Google("g-attacker"));

        // Assert
        Assert.NotEqual(victim, result);
        await using var db = rig.Fresh();
        var victimClaim = await db.UserClaims.SingleAsync(c => c.UserId == victim && c.ClaimType == ProfileClaimTypes.GoogleId);
        Assert.Equal("g-victim", victimClaim.ClaimValue);
        var attackerClaim = await db.UserClaims.SingleAsync(c => c.UserId == result && c.ClaimType == ProfileClaimTypes.GoogleId);
        Assert.Equal("g-attacker", attackerClaim.ClaimValue);
        Assert.True(await db.FiberClients.AnyAsync(c => c.UserId == result));
        Assert.False(await db.FiberClients.AnyAsync(c => c.UserId == victim));
    }

    [Fact]
    public async Task SignIn_WithCookieOfAnUnlinkedProfile_AdoptsItSeedsItAndLinksGoogle()
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
        // Flipped: adoption now seeds (sign-in is where visitors are seeded).
        Assert.True(await db.FiberClients.AnyAsync(c => c.UserId == anon));
        Assert.Equal("g-new", (await db.UserClaims.SingleAsync(c => c.UserId == anon && c.ClaimType == ProfileClaimTypes.GoogleId)).ClaimValue);
    }

    [Fact]
    public async Task SignIn_ExistingButUnseededProfile_IsSeededOnTheNextSignIn()
    {
        // Arrange: a linked profile whose earlier seed never happened.
        using var rig = new SignInRig();
        var existing = await rig.AddProfile("g-old");

        // Act
        var result = await rig.Service.CreateOrUpdateFromGoogleAsync(Google("g-old"));

        // Assert
        Assert.Equal(existing, result);
        await using var db = rig.Fresh();
        Assert.True(await db.FiberClients.AnyAsync(c => c.UserId == existing));
    }

    [Fact]
    public async Task SignIn_Again_ReSeedsNothing()
    {
        // Arrange
        using var rig = new SignInRig();
        await rig.Service.CreateOrUpdateFromGoogleAsync(Google("g-twice"));
        // The rig holds a single user, so whole-table counts are that user's rows.
        int Rows(PortfolioDbContext db) => db.FiberClients.Count() + db.FiberMaterials.Count() + db.FiberOrders.Count()
            + db.FiberShipments.Count() + db.FiberInventoryTransactions.Count() + db.Properties.Count();
        await using var before = rig.Fresh();
        var rowsBefore = Rows(before);

        // Act
        await rig.Service.CreateOrUpdateFromGoogleAsync(Google("g-twice"));

        // Assert
        await using var after = rig.Fresh();
        Assert.True(rowsBefore > 0);
        Assert.Equal(rowsBefore, Rows(after));
    }

    [Fact]
    public async Task SignIn_WhenSeedingFails_StillSucceedsAndLinksTheAccount()
    {
        // Arrange: a seed service over a disposed context throws on first use.
        var options = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseInMemoryDatabase($"Identity_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var db = new PortfolioDbContext(options);
        var broken = new PortfolioDbContext(options);
        await broken.DisposeAsync();
        var service = new UserProfileService(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new UserProfileRepository(db, NullLogger<UserProfileRepository>.Instance),
            TimeProvider.System,
            new UserProfileSeedService(broken, NullLogger<UserProfileSeedService>.Instance),
            NullLogger<UserProfileService>.Instance);

        // Act
        var id = await service.CreateOrUpdateFromGoogleAsync(Google("g-noseed"));

        // Assert
        Assert.True(await db.UserClaims.AnyAsync(c => c.UserId == id && c.ClaimType == ProfileClaimTypes.GoogleId));
        Assert.False(await db.FiberClients.AnyAsync(c => c.UserId == id));
    }
}
