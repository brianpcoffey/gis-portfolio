using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portfolio.Common.Constants;
using Portfolio.Common.Models;
using Portfolio.Repositories;
using Portfolio.Repositories.Diagnostics;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Portfolio.Web.Middleware
{
    /// <summary>
    /// Ensures browser visitors have a UserProfile identity stored in HttpContext.Items["PortfolioIdentity"].
    /// For Google-authenticated users the OnSignedIn event has already set the Items key.
    /// For anonymous users this validates/reuses the AnonUserId cookie, or creates a new profile
    /// (throttled per client address, and never for /api paths). A cookie that names a
    /// Google-linked profile is deleted and gives no identity. Seed data is created at sign-in,
    /// not here.
    /// </summary>
    public class AnonymousUserMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AnonymousUserMiddleware> _logger;
        private const string CookieName = "AnonUserId";
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
            // Only set anonymous identity if not already set by authentication
            if (context.Items.ContainsKey(HttpContextItemKey))
            {
                await _next(context);
                return;
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            Guid userId;

            var authResult = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (authResult.Succeeded && authResult.Principal?.Identity?.IsAuthenticated == true)
            {
                context.User = authResult.Principal;
            }

            if (context.User?.Identity?.IsAuthenticated == true)
            {
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
                await _next(context);
                return;
            }

            // Health probes and non-browser API clients cannot persist cookies, so creating a
            // new profile + seeding for them is wasteful. Only create a new user identity for
            // requests that can actually receive and store a cookie (i.e. browser requests that
            // declare an Accept header containing text/html).
            var acceptsHtml = context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);

            if (context.Request.Cookies.TryGetValue(CookieName, out var cookieValue) && Guid.TryParse(cookieValue, out userId))
            {
                var profile = await db.UserProfiles.AsTracking().FirstOrDefaultAsync(p => p.UserId == userId);
                if (profile != null &&
                    await db.UserClaims.AsNoTracking().AnyAsync(c => c.UserId == userId && c.ClaimType == ProfileClaimTypes.GoogleId))
                {
                    // The anonymous cookie must never act as a Google-linked account: drop it and
                    // continue with no identity (the next browser request gets a fresh one).
                    context.Response.Cookies.Delete(CookieName);
                    await _next(context);
                    return;
                }

                if (profile == null)
                {
                    if (!TryAcquireNewIdentity(context))
                    {
                        await _next(context);
                        return;
                    }

                    profile = new UserProfile
                    {
                        UserId = userId,
                        CreatedDate = now,
                        LastActiveDate = now
                    };
                    db.UserProfiles.Add(profile);
                    await db.SaveChangesAsync();
                }
                else
                {
                    // Throttle the write: only refresh LastActiveDate when it is already stale,
                    // so an active session does not trigger a DB write on every single request.
                    if (now - profile.LastActiveDate >= TimeSpan.FromMinutes(5))
                    {
                        profile.LastActiveDate = now;
                        await db.SaveChangesAsync();
                    }
                }
            }
            else
            {
                // No cookie present. Only create a new profile for browser requests — probes
                // and API clients cannot store the cookie, so creating a profile for them
                // would generate a new anonymous user on every probe request.
                if (!acceptsHtml || !TryAcquireNewIdentity(context))
                {
                    await _next(context);
                    return;
                }

                userId = Guid.NewGuid();
                var profile = new UserProfile
                {
                    UserId = userId,
                    CreatedDate = now,
                    LastActiveDate = now
                };
                db.UserProfiles.Add(profile);
                await db.SaveChangesAsync();
                var cookieOptions = new CookieOptions
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
                context.Response.Cookies.Append(CookieName, userId.ToString(), cookieOptions);
                _logger.LogInformation(
                    "Created anon profile and cookie for {UserId} (Scheme={Scheme})",
                    LogRedaction.UserId(userId),
                    context.Request.Scheme);
            }

            context.Items[HttpContextItemKey] = userId;

            await _next(context);
        }

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