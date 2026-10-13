using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Portfolio.Common.Configuration;

namespace Portfolio.Web.Startup;

internal static class AuthenticationRegistration
{
    internal static IServiceCollection AddPortfolioAuthentication(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            // Challenge with the cookie scheme, NOT Google, so a denied [Authorize] request lands on
            // the app's own /Login page (which offers "Continue with Google") instead of going straight
            // to accounts.google.com. Google is challenged explicitly by the Login page handler.
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
            // Events are assigned below so they can take the registered TimeProvider.
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

            // Defensive: the cookie scheme is the default challenge, so nothing challenges Google on an
            // /api path today. If something ever does, answer 401 rather than a cross-origin redirect to
            // accounts.google.com that the browser's fetch cannot follow. Page navigations still get
            // the normal Google redirect.
            options.Events.OnRedirectToAuthorizationEndpoint = context =>
                PortfolioCookieEvents.ApiAwareRedirect(context, StatusCodes.Status401Unauthorized);
        });

        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<TimeProvider>((options, time) => options.Events = new PortfolioCookieEvents(time));

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Authenticated", policy =>
                policy.RequireAuthenticatedUser());
        });
        return services;
    }
}
