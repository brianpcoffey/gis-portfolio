using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories;

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
        private const string CookieName = "AnonUserId";
        private const string HttpContextItemKey = "PortfolioIdentity";

        public AnonymousUserMiddleware(RequestDelegate next)
        {
            _next = next;
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

            if (context.User?.Identity?.IsAuthenticated == true)
            {
                // Authenticated user: do not set anonymous identity
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
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = timeProvider.GetUtcNow().AddYears(1),
                    IsEssential = true
                };
                context.Response.Cookies.Append(CookieName, userId.ToString(), cookieOptions);
                Console.WriteLine($"[AnonymousUserMiddleware] Created anon profile and cookie for {userId} (IsHttps={context.Request.IsHttps})");
            }

            context.Items[HttpContextItemKey] = userId;

            await _next(context);
        }
    }
}