using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Portfolio.Common.Configuration;

namespace Portfolio.Web.Startup;

internal static class AuthenticationRegistration
{
    // --------------------------
    // Authentication & Authorization
    // --------------------------
    internal static IServiceCollection AddPortfolioAuthentication(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            // Challenge with the cookie scheme, NOT Google. An [Authorize] page or controller
            // that denies an unauthenticated request must land on the app's own /Login page
            // (via the cookie LoginPath / OnRedirectToLogin handler below), which then offers
            // an explicit "Continue with Google" button. Making Google the default challenge
            // scheme instead sends users straight to accounts.google.com — bypassing /Login and
            // surfacing a raw Google error (e.g. redirect_uri_mismatch) — and leaves the cookie
            // OnRedirectToLogin API-vs-page handling as dead code. Google is still invoked
            // explicitly by the Login page handler (Challenge(props, "Google")).
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.LoginPath = "/Login";
            options.LogoutPath = "/Logout";
            // In development allow SameAsRequest so cookies can be used over HTTP (localhost);
            // in production require Secure.
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;

            options.Events = new PortfolioCookieEvents();
        })
        .AddGoogle(options =>
        {
            var googleSettings = configuration.GetSection("Authentication:Google")
                .Get<GoogleAuthSettings>()
                ?? throw new InvalidOperationException("Google auth configuration missing.");

            options.ClientId = googleSettings.ClientId;
            options.ClientSecret = googleSettings.ClientSecret;
            options.CallbackPath = "/signin-google";
            // No feature reads the Google access/id tokens, so do not persist them into the
            // auth cookie (keeps the cookie smaller and avoids holding bearer tokens client-side).
            options.SaveTokens = false;
            options.Scope.Add("profile");
            options.Scope.Add("email");
            options.ClaimActions.MapJsonKey("picture", "picture");

            // Google is the default challenge scheme, so an unauthenticated request to a
            // protected endpoint would otherwise 302 straight to accounts.google.com. For
            // API calls that produces a cross-origin redirect the browser's fetch cannot
            // follow (no CORS headers), surfacing as an opaque failure instead of a clean
            // 401. Return 401 for /api paths so the client can react (redirect to /Login);
            // full-page navigations still get the normal Google redirect.
            options.Events.OnRedirectToAuthorizationEndpoint = context =>
                PortfolioCookieEvents.ApiAwareRedirect(context, StatusCodes.Status401Unauthorized);
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Authenticated", policy =>
                policy.RequireAuthenticatedUser());
        });
        return services;
    }
}
