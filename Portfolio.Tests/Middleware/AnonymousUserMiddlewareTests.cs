using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Net.Http.Headers;
using Moq;
using Portfolio.Common.Constants;
using Portfolio.Common.Models;
using Portfolio.Repositories;
using Portfolio.Web.Middleware;

namespace Portfolio.Tests.Middleware
{
    /// <summary>
    /// Characterisation tests for <see cref="AnonymousUserMiddleware"/>: what it reads, writes and
    /// sets for each request shape.
    /// </summary>
    /// <remarks>
    /// They pin today's behaviour, including the 5-minute LastActiveDate throttle and the cookie
    /// attributes.
    /// </remarks>
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

        private (AnonymousUserMiddleware Middleware, Func<bool> NextCalled) NewMiddleware(string environment, ILogger<AnonymousUserMiddleware>? logger = null)
        {
            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.EnvironmentName).Returns(environment);
            var called = false;
            var middleware = new AnonymousUserMiddleware(
                _ => { called = true; return Task.CompletedTask; },
                env.Object,
                logger ?? NullLogger<AnonymousUserMiddleware>.Instance);
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
            middleware.InvokeAsync(context, _db, _time);

        private static void SetAuthenticated(Mock<IAuthenticationService> auth, ClaimsPrincipal principal) =>
            auth.Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), CookieAuthenticationDefaults.AuthenticationScheme))
                .ReturnsAsync(AuthenticateResult.Success(new AuthenticationTicket(
                    principal, CookieAuthenticationDefaults.AuthenticationScheme)));

        private static ClaimsPrincipal Authenticated(string? nameIdentifier)
        {
            var claims = nameIdentifier is null ? [] : new List<Claim> { new(ClaimTypes.NameIdentifier, nameIdentifier) };
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        [Fact]
        public async Task ItemsAlreadySet_PassesThroughWithoutAuthenticatingOrTouchingTheDatabase()
        {
            // Arrange
            var existing = Guid.NewGuid();
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "text/html");
            context.Items[ItemKey] = existing;

            // Act
            await Invoke(middleware, context);

            // Assert
            Assert.True(nextCalled());
            Assert.Equal(existing, context.Items[ItemKey]);
            _auth.Verify(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string?>()), Times.Never);
            await using var fresh = Fresh();
            Assert.Empty(fresh.UserProfiles);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.SetCookie));
        }

        [Fact]
        public async Task Authenticated_WithMatchingGoogleClaim_SetsItemsToClaimUserId()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _db.UserClaims.Add(new UserClaim { UserId = userId, ClaimType = ProfileClaimTypes.GoogleId, ClaimValue = "g-123" });
            await _db.SaveChangesAsync();
            SetAuthenticated(_auth, Authenticated("g-123"));
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "text/html");

            // Act
            await Invoke(middleware, context);

            // Assert
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
            // Arrange
            SetAuthenticated(_auth, Authenticated("g-unknown"));
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "text/html");

            // Act
            await Invoke(middleware, context);

            // Assert
            Assert.True(nextCalled());
            Assert.False(context.Items.ContainsKey(ItemKey));
            await using var fresh = Fresh();
            Assert.Empty(fresh.UserProfiles);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.SetCookie));
        }

        [Fact]
        public async Task Authenticated_WithoutNameIdentifierClaim_DoesNotSetItems()
        {
            // Arrange
            SetAuthenticated(_auth, Authenticated(null));
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "text/html");

            // Act
            await Invoke(middleware, context);

            // Assert
            Assert.True(nextCalled());
            Assert.False(context.Items.ContainsKey(ItemKey));
        }

        [Fact]
        public async Task Authenticated_ViaContextUserWhenAuthenticateReturnsNothing_ResolvesTheClaim()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _db.UserClaims.Add(new UserClaim { UserId = userId, ClaimType = ProfileClaimTypes.GoogleId, ClaimValue = "g-456" });
            await _db.SaveChangesAsync();
            var (middleware, _) = NewMiddleware("Production");
            var context = NewContext();
            context.User = Authenticated("g-456");

            // Act
            await Invoke(middleware, context);

            // Assert
            Assert.Equal(userId, context.Items[ItemKey]);
        }

        [Fact]
        public async Task Authenticated_ClaimOfAnotherType_IsNotMatched()
        {
            // Arrange
            _db.UserClaims.Add(new UserClaim { UserId = Guid.NewGuid(), ClaimType = ProfileClaimTypes.Email, ClaimValue = "g-123" });
            await _db.SaveChangesAsync();
            SetAuthenticated(_auth, Authenticated("g-123"));
            var (middleware, _) = NewMiddleware("Production");
            var context = NewContext();

            // Act
            await Invoke(middleware, context);

            // Assert
            Assert.False(context.Items.ContainsKey(ItemKey));
        }

        [Fact]
        public async Task Cookie_WithStaleProfile_TouchesLastActiveDate()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var created = Now.UtcDateTime.AddDays(-3);
            var stale = Now.UtcDateTime.AddMinutes(-6);
            _db.UserProfiles.Add(new UserProfile { UserId = userId, CreatedDate = created, LastActiveDate = stale });
            await _db.SaveChangesAsync();
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "application/json", cookie: userId.ToString());

            // Act
            await Invoke(middleware, context);

            // Assert
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
            // Arrange
            var userId = Guid.NewGuid();
            _db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                CreatedDate = Now.UtcDateTime.AddDays(-1),
                LastActiveDate = Now.UtcDateTime.AddMinutes(-5)
            });
            await _db.SaveChangesAsync();
            var (middleware, _) = NewMiddleware("Production");

            // Act
            await Invoke(middleware, NewContext(cookie: userId.ToString()));

            // Assert
            await using var fresh = Fresh();
            Assert.Equal(Now.UtcDateTime, (await fresh.UserProfiles.SingleAsync()).LastActiveDate);
        }

        [Fact]
        public async Task Cookie_WithFreshProfile_DoesNotWrite()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var recent = Now.UtcDateTime.AddMinutes(-4);
            _db.UserProfiles.Add(new UserProfile { UserId = userId, CreatedDate = recent, LastActiveDate = recent });
            await _db.SaveChangesAsync();
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "text/html", cookie: userId.ToString());

            // Act
            await Invoke(middleware, context);

            // Assert
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
            // Arrange
            var userId = Guid.NewGuid();
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: accept, cookie: userId.ToString());

            // Act
            await Invoke(middleware, context);

            // Assert
            Assert.True(nextCalled());
            Assert.Equal(userId, context.Items[ItemKey]);
            await using var fresh = Fresh();
            var profile = await fresh.UserProfiles.SingleAsync();
            Assert.Equal(userId, profile.UserId);
            Assert.Equal(Now.UtcDateTime, profile.CreatedDate);
            Assert.Equal(Now.UtcDateTime, profile.LastActiveDate);
            // The middleware never seeds (seeding moved to sign-in in WP-15).
            Assert.Empty(fresh.FiberClients);
            Assert.Empty(fresh.FiberMaterials);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.SetCookie));
        }

        [Theory]
        [InlineData("text/html")]
        [InlineData("TEXT/HTML,application/xhtml+xml;q=0.9,*/*;q=0.8")]
        public async Task NoCookie_HtmlAccept_CreatesUnseededProfileAndSetsCookie(string accept)
        {
            // Arrange
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: accept);

            // Act
            await Invoke(middleware, context);

            // Assert
            Assert.True(nextCalled());
            var userId = Assert.IsType<Guid>(context.Items[ItemKey]);
            await using var fresh = Fresh();
            var profile = await fresh.UserProfiles.SingleAsync();
            Assert.Equal(userId, profile.UserId);
            Assert.Equal(Now.UtcDateTime, profile.CreatedDate);
            Assert.Equal(Now.UtcDateTime, profile.LastActiveDate);
            // Flipped deliberately (WP-15, D-05): a fresh visit inserts 1 profile row and 0 seed
            // rows; the old assertions required seeded fiber clients and materials.
            Assert.Empty(fresh.FiberClients);
            Assert.Empty(fresh.FiberMaterials);

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
            // Arrange
            var (middleware, _) = NewMiddleware("Development");
            var context = NewContext(accept: "text/html");

            // Act
            await Invoke(middleware, context);

            // Assert
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
            // Arrange
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: accept);

            // Act
            await Invoke(middleware, context);

            // Assert
            Assert.True(nextCalled());
            Assert.False(context.Items.ContainsKey(ItemKey));
            await using var fresh = Fresh();
            Assert.Empty(fresh.UserProfiles);
            Assert.Empty(fresh.FiberClients);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.SetCookie));
        }

        [Fact]
        public async Task InvalidGuidCookie_HtmlAccept_CreatesNewUnseededProfileAndCookie()
        {
            // Arrange
            var (middleware, _) = NewMiddleware("Production");
            var context = NewContext(accept: "text/html", cookie: "not-a-guid");

            // Act
            await Invoke(middleware, context);

            // Assert
            var userId = Assert.IsType<Guid>(context.Items[ItemKey]);
            await using var fresh = Fresh();
            Assert.Equal(userId, (await fresh.UserProfiles.SingleAsync()).UserId);
            Assert.Empty(fresh.FiberClients); // flipped (WP-15): no seeding at first visit
            var setCookie = SetCookieHeaderValue.Parse(context.Response.Headers[HeaderNames.SetCookie].ToString());
            Assert.Equal(userId.ToString(), setCookie.Value.Value);
        }

        [Fact]
        public async Task InvalidGuidCookie_JsonAccept_PassesThroughWithoutProfileOrCookie()
        {
            // Arrange
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "application/json", cookie: "not-a-guid");

            // Act
            await Invoke(middleware, context);

            // Assert
            Assert.True(nextCalled());
            Assert.False(context.Items.ContainsKey(ItemKey));
            await using var fresh = Fresh();
            Assert.Empty(fresh.UserProfiles);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.SetCookie));
        }

        [Fact]
        public async Task Cookie_ForProfileLinkedToGoogle_GivesNoIdentity_AndDeletesTheCookie()
        {
            // Arrange
            // Flipped from the WP-15 characterisation, where the linked profile's identity was
            // honoured for an unauthenticated request.
            var victim = Guid.NewGuid();
            _db.UserProfiles.Add(new UserProfile { UserId = victim, CreatedDate = Now.UtcDateTime, LastActiveDate = Now.UtcDateTime });
            _db.UserClaims.Add(new UserClaim { UserId = victim, ClaimType = ProfileClaimTypes.GoogleId, ClaimValue = "g-victim" });
            await _db.SaveChangesAsync();
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "text/html", cookie: victim.ToString());

            // Act
            await Invoke(middleware, context);

            // Assert
            Assert.True(nextCalled());
            Assert.False(context.Items.ContainsKey(ItemKey));
            var setCookie = SetCookieHeaderValue.Parse(context.Response.Headers[HeaderNames.SetCookie].ToString());
            Assert.Equal(CookieName, setCookie.Name.Value);
            Assert.True(string.IsNullOrEmpty(setCookie.Value.Value));
            Assert.True(setCookie.Expires < Now);
            await using var fresh = Fresh();
            Assert.Equal(victim, (await fresh.UserProfiles.SingleAsync()).UserId);
        }

        [Fact]
        public async Task Cookie_ForProfileLinkedToGoogle_DoesNotTouchLastActiveDate()
        {
            // Arrange
            var victim = Guid.NewGuid();
            var stale = Now.UtcDateTime.AddHours(-1);
            _db.UserProfiles.Add(new UserProfile { UserId = victim, CreatedDate = stale, LastActiveDate = stale });
            _db.UserClaims.Add(new UserClaim { UserId = victim, ClaimType = ProfileClaimTypes.GoogleId, ClaimValue = "g-victim" });
            await _db.SaveChangesAsync();
            var (middleware, _) = NewMiddleware("Production");

            // Act
            await Invoke(middleware, NewContext(cookie: victim.ToString()));

            // Assert
            await using var fresh = Fresh();
            Assert.Equal(stale, (await fresh.UserProfiles.SingleAsync()).LastActiveDate);
        }

        [Fact]
        public async Task Cookie_ForProfileWithOnlyNonGoogleClaims_IsStillHonoured()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _db.UserProfiles.Add(new UserProfile { UserId = userId, CreatedDate = Now.UtcDateTime, LastActiveDate = Now.UtcDateTime });
            _db.UserClaims.Add(new UserClaim { UserId = userId, ClaimType = ProfileClaimTypes.DisplayName, ClaimValue = "Ann" });
            await _db.SaveChangesAsync();
            var (middleware, _) = NewMiddleware("Production");
            var context = NewContext(cookie: userId.ToString());

            // Act
            await Invoke(middleware, context);

            // Assert
            Assert.Equal(userId, context.Items[ItemKey]);
        }

        [Theory]
        [InlineData("/api/v1/features/saved")]
        [InlineData("/API/v1/features/saved")]
        public async Task NoCookie_HtmlAccept_OnApiPath_CreatesNoProfile(string path)
        {
            // Arrange
            // Flipped from the WP-15 characterisation, where /api with Accept text/html created one.
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "text/html");
            context.Request.Path = path;

            // Act
            await Invoke(middleware, context);

            // Assert
            Assert.True(nextCalled());
            Assert.False(context.Items.ContainsKey(ItemKey));
            await using var fresh = Fresh();
            Assert.Empty(fresh.UserProfiles);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.SetCookie));
        }

        [Fact]
        public async Task UnknownGuidCookie_OnApiPath_CreatesNoProfile()
        {
            // Arrange
            var (middleware, nextCalled) = NewMiddleware("Production");
            var context = NewContext(accept: "application/json", cookie: Guid.NewGuid().ToString());
            context.Request.Path = "/api/v1/features/saved";

            // Act
            await Invoke(middleware, context);

            // Assert
            Assert.True(nextCalled());
            Assert.False(context.Items.ContainsKey(ItemKey));
            await using var fresh = Fresh();
            Assert.Empty(fresh.UserProfiles);
        }

        [Fact]
        public async Task Cookie_ForExistingProfile_OnApiPath_IsStillHonoured()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _db.UserProfiles.Add(new UserProfile { UserId = userId, CreatedDate = Now.UtcDateTime, LastActiveDate = Now.UtcDateTime });
            await _db.SaveChangesAsync();
            var (middleware, _) = NewMiddleware("Production");
            var context = NewContext(accept: "application/json", cookie: userId.ToString());
            context.Request.Path = "/api/v1/features/saved";

            // Act
            await Invoke(middleware, context);

            // Assert
            Assert.Equal(userId, context.Items[ItemKey]);
        }

        private DefaultHttpContext FromAddress(string ip, string? cookie = null)
        {
            var context = NewContext(accept: "text/html", cookie: cookie);
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip);
            return context;
        }

        [Fact]
        public async Task NoCookie_EleventhNewIdentityFromOneAddress_GetsNoIdentityAndNoCookie()
        {
            // Arrange
            // Flipped from the WP-15 characterisation, where twelve were all created.
            var (middleware, _) = NewMiddleware("Production");

            // Act
            for (var i = 0; i < 10; i++)
            {
                var ok = FromAddress("203.0.113.9");
                await Invoke(middleware, ok);
                Assert.IsType<Guid>(ok.Items[ItemKey]);
            }
            var denied = FromAddress("203.0.113.9");
            await Invoke(middleware, denied);

            // Assert
            Assert.False(denied.Items.ContainsKey(ItemKey));
            Assert.False(denied.Response.Headers.ContainsKey(HeaderNames.SetCookie));
            await using var fresh = Fresh();
            Assert.Equal(10, await fresh.UserProfiles.CountAsync());
        }

        [Fact]
        public async Task NoCookie_TheLimitIsPerAddress()
        {
            // Arrange
            var (middleware, _) = NewMiddleware("Production");
            for (var i = 0; i < 10; i++)
                await Invoke(middleware, FromAddress("203.0.113.9"));

            // Act
            var other = FromAddress("203.0.113.10");
            await Invoke(middleware, other);

            // Assert
            Assert.IsType<Guid>(other.Items[ItemKey]);
        }

        [Fact]
        public async Task UnknownGuidCookie_SharesTheSameLimitAsTheNoCookiePath()
        {
            // Arrange
            var (middleware, _) = NewMiddleware("Production");
            for (var i = 0; i < 5; i++)
                await Invoke(middleware, FromAddress("203.0.113.9"));
            for (var i = 0; i < 5; i++)
                await Invoke(middleware, FromAddress("203.0.113.9", cookie: Guid.NewGuid().ToString()));

            // Act
            var denied = FromAddress("203.0.113.9", cookie: Guid.NewGuid().ToString());
            await Invoke(middleware, denied);

            // Assert
            Assert.False(denied.Items.ContainsKey(ItemKey));
            await using var fresh = Fresh();
            Assert.Equal(10, await fresh.UserProfiles.CountAsync());
        }

        [Fact]
        public async Task ExistingProfileCookie_IsNotCountedAgainstTheLimit()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _db.UserProfiles.Add(new UserProfile { UserId = userId, CreatedDate = Now.UtcDateTime, LastActiveDate = Now.UtcDateTime });
            await _db.SaveChangesAsync();
            var (middleware, _) = NewMiddleware("Production");
            for (var i = 0; i < 10; i++)
                await Invoke(middleware, FromAddress("203.0.113.9"));

            // Act
            var context = FromAddress("203.0.113.9", cookie: userId.ToString());
            await Invoke(middleware, context);

            // Assert
            Assert.Equal(userId, context.Items[ItemKey]);
        }

        [Fact]
        public async Task CreatingAnIdentity_LogsOnlyTheRedactedUserId()
        {
            // Arrange
            var logger = new CapturingLogger();
            var (middleware, _) = NewMiddleware("Production", logger);
            var context = NewContext(accept: "text/html");

            // Act
            await Invoke(middleware, context);

            // Assert
            var userId = (Guid)context.Items[ItemKey]!;
            var line = Assert.Single(logger.Lines);
            Assert.DoesNotContain(userId.ToString(), line, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Portfolio.Repositories.Diagnostics.LogRedaction.UserId(userId), line, StringComparison.Ordinal);
        }

        private sealed class CapturingLogger : ILogger<AnonymousUserMiddleware>
        {
            public List<string> Lines { get; } = [];
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                Lines.Add(formatter(state, exception));
        }
    }
}
