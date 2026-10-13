using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Constants;
using Portfolio.Common.Models;
using Portfolio.Repositories;
using Portfolio.Repositories.Diagnostics;

namespace Portfolio.Web.Middleware
{
    /// <summary>
    /// Ensures browser visitors have a UserProfile identity stored in HttpContext.Items["PortfolioIdentity"].
    /// </summary>
    /// <remarks>
    /// This middleware runs before UseAuthentication, so it authenticates the cookie scheme itself: a
    /// signed-in Google user is resolved to their profile through the GoogleId claim. For anonymous
    /// users it validates/reuses the AnonUserId cookie, or creates a new profile (throttled per client
    /// address, and never for /api paths). A cookie that names a Google-linked profile is deleted and
    /// gives no identity. Seed data is created at sign-in, not here.
    /// </remarks>
    public class AnonymousUserMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AnonymousUserMiddleware> _logger;
        // Shared with ApiRequestHeaderMiddleware, which must recognise this cookie.
        internal const string CookieName = "AnonUserId";
        private const string HttpContextItemKey = "PortfolioIdentity";

        // New anonymous identities per client address per minute. An instance field: the
        // middleware is constructed once per application, so this is app-wide state.
        private const int NewIdentitiesPerMinute = 10;
        private readonly PartitionedRateLimiter<string> _identityLimiter =
            PartitionedRateLimiter.Create<string, string>(ip =>
                RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = NewIdentitiesPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        public AnonymousUserMiddleware(
            RequestDelegate next,
            IWebHostEnvironment environment,
            ILogger<AnonymousUserMiddleware> logger)
        {
            _next = next;
            _environment = environment;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, PortfolioDbContext db, TimeProvider timeProvider)
        {
            // Nothing to do when an identity is already stored for this request.
            if (context.Items.ContainsKey(HttpContextItemKey))
            {
                await _next(context);
                return;
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            Guid userId;

            if (await TryResolveAuthenticatedAsync(context, db))
            {
                await _next(context);
                return;
            }

            // Health probes and non-browser API clients cannot persist cookies, so a profile created
            // for them would be orphaned and every probe request would create a new anonymous user.
            // Only browser requests (an Accept header containing text/html) get a new identity.
            var acceptsHtml = context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);

            if (context.Request.Cookies.TryGetValue(CookieName, out var cookieValue) && Guid.TryParse(cookieValue, out userId))
            {
                if (!await ResolveFromCookieAsync(context, db, userId, now))
                {
                    await _next(context);
                    return;
                }
            }
            else
            {
                if (!acceptsHtml || !TryAcquireNewIdentity(context))
                {
                    await _next(context);
                    return;
                }

                userId = Guid.NewGuid();
                await CreateAnonymousAsync(db, userId, now);
                context.Response.Cookies.Append(CookieName, userId.ToString(), BuildCookieOptions(timeProvider));
                _logger.LogInformation(
                    "Created anon profile and cookie for {UserId} (Scheme={Scheme})",
                    LogRedaction.UserId(userId),
                    LogRedaction.SingleLine(context.Request.Scheme));
            }

            context.Items[HttpContextItemKey] = userId;

            await _next(context);
        }

        // Handles a well-formed anonymous cookie. Returns true when the request should continue
        // with this userId as its identity; false when it must continue with no identity (the
        // cookie names a Google-linked profile and is dropped, or an unknown id is throttled).
        private async Task<bool> ResolveFromCookieAsync(HttpContext context, PortfolioDbContext db, Guid userId, DateTime now)
        {
            var profile = await db.UserProfiles.AsTracking().FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile != null &&
                await db.UserClaims.AsNoTracking().AnyAsync(c => c.UserId == userId && c.ClaimType == ProfileClaimTypes.GoogleId))
            {
                // The anonymous cookie must never act as a Google-linked account: drop it and
                // continue with no identity (the next browser request gets a fresh one).
                context.Response.Cookies.Delete(CookieName);
                return false;
            }

            if (profile == null)
            {
                if (!TryAcquireNewIdentity(context))
                    return false;

                await CreateAnonymousAsync(db, userId, now);
            }
            else
            {
                await TouchProfileAsync(db, profile, now);
            }
            return true;
        }

        // Returns true when the request is authenticated (the caller then just continues the
        // pipeline). The identity is stored only if the principal's GoogleId maps to a profile.
        private static async Task<bool> TryResolveAuthenticatedAsync(HttpContext context, PortfolioDbContext db)
        {
            var authResult = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (authResult.Succeeded && authResult.Principal?.Identity?.IsAuthenticated == true)
            {
                context.User = authResult.Principal;
            }

            if (context.User?.Identity?.IsAuthenticated != true)
                return false;

            // Authenticated user: resolve their internal UserId from the GoogleId claim and store it.
            var googleId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(googleId))
            {
                var claim = await db.UserClaims
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ClaimType == ProfileClaimTypes.GoogleId && c.ClaimValue == googleId);
                if (claim != null)
                    context.Items[HttpContextItemKey] = claim.UserId;
            }
            return true;
        }

        private static async Task TouchProfileAsync(PortfolioDbContext db, UserProfile profile, DateTime now)
        {
            // Throttle the write: only refresh LastActiveDate when it is already stale,
            // so an active session does not trigger a DB write on every single request.
            if (now - profile.LastActiveDate >= TimeSpan.FromMinutes(5))
            {
                profile.LastActiveDate = now;
                await db.SaveChangesAsync();
            }
        }

        private static async Task CreateAnonymousAsync(PortfolioDbContext db, Guid userId, DateTime now)
        {
            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                CreatedDate = now,
                LastActiveDate = now
            });
            await db.SaveChangesAsync();
        }

        private CookieOptions BuildCookieOptions(TimeProvider timeProvider) => new()
        {
            HttpOnly = true,
            // Always Secure in production: Render terminates TLS at the proxy, so
            // Request.IsHttps is false for internal health probes even on a live HTTPS
            // deployment. Tying Secure to IsHttps would produce a non-Secure cookie
            // whenever a probe triggers the new-user path. Use the environment instead.
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Expires = timeProvider.GetUtcNow().AddYears(1),
            IsEssential = true
        };

        // Gate for both identity-creation paths. API paths never create an identity, and each
        // client address may create NewIdentitiesPerMinute new ones per minute.
        private bool TryAcquireNewIdentity(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
                return false;

            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            using var lease = _identityLimiter.AttemptAcquire(ip);
            return lease.IsAcquired;
        }
    }
}
