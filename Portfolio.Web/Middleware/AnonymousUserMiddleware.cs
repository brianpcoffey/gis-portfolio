using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portfolio.Common.Constants;
using Portfolio.Common.Models;
using Portfolio.Repositories;
using System.Security.Claims;

namespace Portfolio.Web.Middleware
{
    /// <summary>
    /// Ensures every visitor has a UserProfile identity stored in HttpContext.Items["AnonUserId"].
    /// For Google-authenticated users the OnSignedIn event has already set the cookie and Items key.
    /// For anonymous users this validates/reuses the AnonUserId cookie, or creates a new profile.
    /// </summary>
    public class AnonymousUserMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AnonymousUserMiddleware> _logger;
        private const string CookieName = "AnonUserId";
        private const string HttpContextItemKey = "PortfolioIdentity";

        public AnonymousUserMiddleware(
            RequestDelegate next,
            IWebHostEnvironment environment,
            ILogger<AnonymousUserMiddleware> logger)
        {
            _next = next;
            _environment = environment;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, PortfolioDbContext db, TimeProvider timeProvider, Portfolio.Services.Services.UserProfileSeedService seedService)
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

            if (context.Request.Cookies.TryGetValue(CookieName, out var cookieValue) && Guid.TryParse(cookieValue, out userId))
            {
                var profile = await db.UserProfiles.AsTracking().FirstOrDefaultAsync(p => p.UserId == userId);
                if (profile == null)
                {
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
                    profile.LastActiveDate = now;
                    await db.SaveChangesAsync();
                }
            }
            else
            {
                userId = Guid.NewGuid();
                var profile = new UserProfile
                {
                    UserId = userId,
                    CreatedDate = now,
                    LastActiveDate = now
                };
                db.UserProfiles.Add(profile);
                await db.SaveChangesAsync();
                await seedService.SeedForUserAsync(userId);
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
                    userId,
                    context.Request.Scheme);
            }

            context.Items[HttpContextItemKey] = userId;

            await _next(context);
        }
    }
}