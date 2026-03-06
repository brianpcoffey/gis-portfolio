using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories;

namespace Portfolio.Web.Middleware
{
    /// <summary>
    /// Ensures every visitor has a UserProfile identity stored in HttpContext.Items["AnonUserId"].
    /// For Google-authenticated users: the OnSignedIn event has already set the cookie and Items key.
    /// For anonymous users: validates/reuses the AnonUserId cookie, or creates a new profile.
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
            // If the OnSignedIn event already resolved the user for this request, skip
            if (context.Items.ContainsKey(HttpContextItemKey))
            {
                await _next(context);
                return;
            }

            Guid userId;

            // Try read cookie
            if (context.Request.Cookies.TryGetValue(CookieName, out var cookieValue) && Guid.TryParse(cookieValue, out userId))
            {
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
                    profile.LastActiveDate = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
            else
            {
                userId = Guid.NewGuid();

                var profile = new UserProfile
                {
                    UserId = userId,
                    CreatedDate = DateTime.UtcNow,
                    LastActiveDate = DateTime.UtcNow
                };

                db.UserProfiles.Add(profile);
                await db.SaveChangesAsync();

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

            context.Items[HttpContextItemKey] = userId;

            await _next(context);
        }
    }
}