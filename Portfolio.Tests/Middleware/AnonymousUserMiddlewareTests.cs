using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Net.Http.Headers;
using Moq;
using Portfolio.Common.Constants;
using Portfolio.Common.Models;
using Portfolio.Repositories;
using Portfolio.Services.Services;
using Portfolio.Web.Middleware;

namespace Portfolio.Tests.Middleware
{
    /// <summary>
    /// Characterisation tests for <see cref="AnonymousUserMiddleware"/>: what it reads, writes and
    /// sets for each request shape. They pin today's behaviour, including the 5-minute
    /// LastActiveDate throttle and the cookie attributes.
    /// </summary>
    public class AnonymousUserMiddlewareTests : IDisposable
    {
        private const string ItemKey = "PortfolioIdentity";
        private const string CookieName = "AnonUserId";

        private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly DbContextOptions<PortfolioDbContext> _options;
        private readonly PortfolioDbContext _db;
        private readonly FakeTimeProvider _time = new(Now);
        private readonly Mock<IAuthenticationService> _auth = new();

        public AnonymousUserMiddlewareTests()
        {
            _options = new DbContextOptionsBuilder<PortfolioDbContext>()
                .UseInMemoryDatabase($"AnonMiddleware_{Guid.NewGuid()}")
                .Options;
            _db = new PortfolioDbContext(_options);
            _auth.Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), CookieAuthenticationDefaults.AuthenticationScheme))
                .ReturnsAsync(AuthenticateResult.NoResult());
        }

        public void Dispose() => _db.Dispose();

        // A second context over the same store, so assertions see what was really saved.
        private PortfolioDbContext Fresh() => new(_options);

        private (AnonymousUserMiddleware Middleware, Func<bool> NextCalled) NewMiddleware(string environment)
        {
            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.EnvironmentName).Returns(environment);
            var called = false;
            var middleware = new AnonymousUserMiddleware(
                _ => { called = true; return Task.CompletedTask; },
                env.Object,
                NullLogger<AnonymousUserMiddleware>.Instance);
            return (middleware, () => called);
        }

        private DefaultHttpContext NewContext(string? accept = null, string? cookie = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(_auth.Object);
            var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
            if (accept != null)
                context.Request.Headers.Accept = accept;
            if (cookie != null)
                context.Request.Headers.Cookie = $"{CookieName}={cookie}";
            return context;
        }

        private Task Invoke(AnonymousUserMiddleware middleware, HttpContext context) =>
            middleware.InvokeAsync(context, _db, _time, new UserProfileSeedService(_db, NullLogger<UserProfileSeedService>.Instance));

        private static void SetAuthenticated(Mock<IAuthenticationService> auth, ClaimsPrincipal principal) =>
            auth.Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), CookieAuthenticationDefaults.AuthenticationScheme))
                .ReturnsAsync(AuthenticateResult.Success(new AuthenticationTicket(
                    principal, CookieAuthenticationDefaults.AuthenticationScheme)));

        private static ClaimsPrincipal Authenticated(string? nameIdentifier)
        {
            var claims = nameIdentifier is null ? [] : new List<Claim> { new(ClaimTypes.NameIdentifier, nameIdentifier) };
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        // ── Items already set ───────────────────────────────────────────────────

        [Fact]
        public async Task ItemsAlreadySet_PassesThroughWithoutAuthenticatingOrTouchingTheDatabase()
        {
            var existing = Guid.NewGuid();
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "text/html");
            context.Items[ItemKey] = existing;

            await Invoke(middleware, context);

            Assert.True(nextCalled());
            Assert.Equal(existing, context.Items[ItemKey]);
            _auth.Verify(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string?>()), Times.Never);
            await using var fresh = Fresh();
            Assert.Empty(fresh.UserProfiles);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.SetCookie));
        }

        // ── Authenticated users ─────────────────────────────────────────────────

        [Fact]
        public async Task Authenticated_WithMatchingGoogleClaim_SetsItemsToClaimUserId()
        {
            var userId = Guid.NewGuid();
            _db.UserClaims.Add(new UserClaim { UserId = userId, ClaimType = ProfileClaimTypes.GoogleId, ClaimValue = "g-123" });
            await _db.SaveChangesAsync();
            SetAuthenticated(_auth, Authenticated("g-123"));
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "text/html");

            await Invoke(middleware, context);

            Assert.True(nextCalled());
            Assert.Equal(userId, context.Items[ItemKey]);
            Assert.True(context.User.Identity!.IsAuthenticated);
            // An authenticated visitor never gets an anonymous profile or cookie.
            await using var fresh = Fresh();
            Assert.Empty(fresh.UserProfiles);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.SetCookie));
        }

        [Fact]
        public async Task Authenticated_WithoutMatchingClaimRow_DoesNotSetItems()
        {
            SetAuthenticated(_auth, Authenticated("g-unknown"));
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "text/html");

            await Invoke(middleware, context);

            Assert.True(nextCalled());
            Assert.False(context.Items.ContainsKey(ItemKey));
            await using var fresh = Fresh();
            Assert.Empty(fresh.UserProfiles);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.SetCookie));
        }

        [Fact]
        public async Task Authenticated_WithoutNameIdentifierClaim_DoesNotSetItems()
        {
            SetAuthenticated(_auth, Authenticated(null));
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "text/html");

            await Invoke(middleware, context);

            Assert.True(nextCalled());
            Assert.False(context.Items.ContainsKey(ItemKey));
        }

        [Fact]
        public async Task Authenticated_ViaContextUserWhenAuthenticateReturnsNothing_ResolvesTheClaim()
        {
            var userId = Guid.NewGuid();
            _db.UserClaims.Add(new UserClaim { UserId = userId, ClaimType = ProfileClaimTypes.GoogleId, ClaimValue = "g-456" });
            await _db.SaveChangesAsync();
            var (middleware, _) = NewMiddleware("Production");
            var context = NewContext();
            context.User = Authenticated("g-456");

            await Invoke(middleware, context);

            Assert.Equal(userId, context.Items[ItemKey]);
        }

        [Fact]
        public async Task Authenticated_ClaimOfAnotherType_IsNotMatched()
        {
            _db.UserClaims.Add(new UserClaim { UserId = Guid.NewGuid(), ClaimType = ProfileClaimTypes.Email, ClaimValue = "g-123" });
            await _db.SaveChangesAsync();
            SetAuthenticated(_auth, Authenticated("g-123"));
            var (middleware, _) = NewMiddleware("Production");
            var context = NewContext();

            await Invoke(middleware, context);

            Assert.False(context.Items.ContainsKey(ItemKey));
        }

        // ── Valid anonymous cookie ──────────────────────────────────────────────

        [Fact]
        public async Task Cookie_WithStaleProfile_TouchesLastActiveDate()
        {
            var userId = Guid.NewGuid();
            var created = Now.UtcDateTime.AddDays(-3);
            var stale = Now.UtcDateTime.AddMinutes(-6);
            _db.UserProfiles.Add(new UserProfile { UserId = userId, CreatedDate = created, LastActiveDate = stale });
            await _db.SaveChangesAsync();
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "application/json", cookie: userId.ToString());

            await Invoke(middleware, context);

            Assert.True(nextCalled());
            Assert.Equal(userId, context.Items[ItemKey]);
            await using var fresh = Fresh();
            var profile = await fresh.UserProfiles.SingleAsync();
            Assert.Equal(Now.UtcDateTime, profile.LastActiveDate);
            Assert.Equal(created, profile.CreatedDate);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.SetCookie));
        }

        [Fact]
        public async Task Cookie_WithProfileExactlyFiveMinutesOld_IsTouched()
        {
            var userId = Guid.NewGuid();
            _db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                CreatedDate = Now.UtcDateTime.AddDays(-1),
                LastActiveDate = Now.UtcDateTime.AddMinutes(-5)
            });
            await _db.SaveChangesAsync();
            var (middleware, _) = NewMiddleware("Production");

            await Invoke(middleware, NewContext(cookie: userId.ToString()));

            await using var fresh = Fresh();
            Assert.Equal(Now.UtcDateTime, (await fresh.UserProfiles.SingleAsync()).LastActiveDate);
        }

        [Fact]
        public async Task Cookie_WithFreshProfile_DoesNotWrite()
        {
            var userId = Guid.NewGuid();
            var recent = Now.UtcDateTime.AddMinutes(-4);
            _db.UserProfiles.Add(new UserProfile { UserId = userId, CreatedDate = recent, LastActiveDate = recent });
            await _db.SaveChangesAsync();
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "text/html", cookie: userId.ToString());

            await Invoke(middleware, context);

            Assert.True(nextCalled());
            Assert.Equal(userId, context.Items[ItemKey]);
            await using var fresh = Fresh();
            Assert.Equal(recent, (await fresh.UserProfiles.SingleAsync()).LastActiveDate);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.SetCookie));
        }

        [Theory]
        [InlineData("text/html")]
        [InlineData("application/json")]
        [InlineData(null)]
        public async Task Cookie_WithNoProfile_CreatesProfileWithoutSeedingOrReissuingCookie(string? accept)
        {
            var userId = Guid.NewGuid();
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: accept, cookie: userId.ToString());

            await Invoke(middleware, context);

            Assert.True(nextCalled());
            Assert.Equal(userId, context.Items[ItemKey]);
            await using var fresh = Fresh();
            var profile = await fresh.UserProfiles.SingleAsync();
            Assert.Equal(userId, profile.UserId);
            Assert.Equal(Now.UtcDateTime, profile.CreatedDate);
            Assert.Equal(Now.UtcDateTime, profile.LastActiveDate);
            // Characterisation: the seed runs only in the no-cookie branch.
            Assert.Empty(fresh.FiberClients);
            Assert.Empty(fresh.FiberMaterials);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.SetCookie));
        }

        // ── No cookie ───────────────────────────────────────────────────────────

        [Theory]
        [InlineData("text/html")]
        [InlineData("TEXT/HTML,application/xhtml+xml;q=0.9,*/*;q=0.8")]
        public async Task NoCookie_HtmlAccept_CreatesSeededProfileAndSetsCookie(string accept)
        {
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: accept);

            await Invoke(middleware, context);

            Assert.True(nextCalled());
            var userId = Assert.IsType<Guid>(context.Items[ItemKey]);
            await using var fresh = Fresh();
            var profile = await fresh.UserProfiles.SingleAsync();
            Assert.Equal(userId, profile.UserId);
            Assert.Equal(Now.UtcDateTime, profile.CreatedDate);
            Assert.Equal(Now.UtcDateTime, profile.LastActiveDate);
            Assert.True(await fresh.FiberClients.CountAsync(c => c.UserId == userId) > 0);
            Assert.True(await fresh.FiberMaterials.CountAsync(m => m.UserId == userId) > 0);

            var setCookie = SetCookieHeaderValue.Parse(context.Response.Headers[HeaderNames.SetCookie].ToString());
            Assert.Equal(CookieName, setCookie.Name.Value);
            Assert.Equal(userId.ToString(), setCookie.Value.Value);
            Assert.True(setCookie.HttpOnly);
            Assert.True(setCookie.Secure);
            Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, setCookie.SameSite);
            Assert.Equal(Now.AddYears(1), setCookie.Expires);
        }

        [Fact]
        public async Task NoCookie_HtmlAccept_InDevelopment_CookieIsNotSecure()
        {
            var (middleware, _) = NewMiddleware("Development");
            var context = NewContext(accept: "text/html");

            await Invoke(middleware, context);

            var setCookie = SetCookieHeaderValue.Parse(context.Response.Headers[HeaderNames.SetCookie].ToString());
            Assert.False(setCookie.Secure);
            Assert.True(setCookie.HttpOnly);
            Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, setCookie.SameSite);
        }

        [Theory]
        [InlineData("application/json")]
        [InlineData("*/*")]
        [InlineData(null)]
        public async Task NoCookie_NonHtmlAccept_PassesThroughWithoutProfileOrCookie(string? accept)
        {
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: accept);

            await Invoke(middleware, context);

            Assert.True(nextCalled());
            Assert.False(context.Items.ContainsKey(ItemKey));
            await using var fresh = Fresh();
            Assert.Empty(fresh.UserProfiles);
            Assert.Empty(fresh.FiberClients);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.SetCookie));
        }

        // ── Invalid GUID cookie behaves like no cookie ──────────────────────────

        [Fact]
        public async Task InvalidGuidCookie_HtmlAccept_CreatesNewSeededProfileAndCookie()
        {
            var (middleware, _) = NewMiddleware("Production");
            var context = NewContext(accept: "text/html", cookie: "not-a-guid");

            await Invoke(middleware, context);

            var userId = Assert.IsType<Guid>(context.Items[ItemKey]);
            await using var fresh = Fresh();
            Assert.Equal(userId, (await fresh.UserProfiles.SingleAsync()).UserId);
            Assert.True(await fresh.FiberClients.AnyAsync(c => c.UserId == userId));
            var setCookie = SetCookieHeaderValue.Parse(context.Response.Headers[HeaderNames.SetCookie].ToString());
            Assert.Equal(userId.ToString(), setCookie.Value.Value);
        }

        [Fact]
        public async Task InvalidGuidCookie_JsonAccept_PassesThroughWithoutProfileOrCookie()
        {
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "application/json", cookie: "not-a-guid");

            await Invoke(middleware, context);

            Assert.True(nextCalled());
            Assert.False(context.Items.ContainsKey(ItemKey));
            await using var fresh = Fresh();
            Assert.Empty(fresh.UserProfiles);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.SetCookie));
        }
    }
}
