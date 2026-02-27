using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories;

namespace Portfolio.Web.Middleware
{
    /// <summary>
    /// Ensures every visitor has an anonymous GUID-based identity stored in a secure cookie.
    /// Responsibility:
    ///  - Validate/reuse cookie
    ///  - If missing/invalid, create new UserProfile and set cookie
    ///  - Load profile and update LastActiveDate
    ///  - Place the current UserId into HttpContext.Items["AnonUserId"]
    /// </summary>
    public class AnonymousUserMiddleware
    {
        private readonly RequestDelegate _next;
        private const string CookieName = "AnonUserId";
        private const string HttpContextItemKey = "AnonUserId";

        public AnonymousUserMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, PortfolioDbContext db)
        {
            Guid userId;

            // 1. Try read cookie
            if (context.Request.Cookies.TryGetValue(CookieName, out var cookieValue) && Guid.TryParse(cookieValue, out userId))
            {
                // Load profile; if missing on DB side, recreate profile
                var profile = await db.UserProfiles.AsTracking().FirstOrDefaultAsync(p => p.UserId == userId);
                if (profile == null)
                {
                    profile = new UserProfile
                    {
                        UserId = userId,
                        CreatedDate = DateTime.UtcNow,
                        LastActiveDate = DateTime.UtcNow
                    };
                    db.UserProfiles.Add(profile);
                    await db.SaveChangesAsync();
                }
                else
                {
                    // update last active
                    profile.LastActiveDate = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
            else
            {
                // Cookie missing or invalid -> generate new GUID, create profile, set cookie
                userId = Guid.NewGuid();

                var profile = new UserProfile
                {
                    UserId = userId,
                    CreatedDate = DateTime.UtcNow,
                    LastActiveDate = DateTime.UtcNow
                };

                db.UserProfiles.Add(profile);
                await db.SaveChangesAsync();

                // Set secure cookie
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true
                };

                context.Response.Cookies.Append(CookieName, userId.ToString(), cookieOptions);
            }

            // Store in HttpContext.Items so services/controllers can read it (never accept UserId from clients).
            context.Items[HttpContextItemKey] = userId;

            await _next(context);
        }
    }
}