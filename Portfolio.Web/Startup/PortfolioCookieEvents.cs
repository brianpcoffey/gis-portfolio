using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Startup;

/// <summary>
/// Cookie-authentication events: API-aware redirects and the post-sign-in profile merge.
/// </summary>
internal sealed class PortfolioCookieEvents : CookieAuthenticationEvents
{
    // -------------------------------------------------------
    // Return 401/403 for API requests instead of a 302 to the /Login
    // page, so fetch/XHR callers get a status they can react to
    // (the client then sends the user to /Login) instead of silently
    // following the redirect and parsing the login page as JSON.
    //
    // Non-API (Razor Pages) requests still get the normal 302 redirect.
    // -------------------------------------------------------
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) =>
        ApiAwareRedirect(context, StatusCodes.Status401Unauthorized);

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
        ApiAwareRedirect(context, StatusCodes.Status403Forbidden);

    public override async Task SignedIn(CookieSignedInContext context)
    {
        var principal = context.Principal;
        if (principal?.Identity?.IsAuthenticated != true)
            return;

        var googleId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email);
        var name = principal.FindFirstValue(ClaimTypes.Name);
        var picture = principal.FindFirstValue("picture");

        if (string.IsNullOrEmpty(googleId))
            return;

        var profileService = context.HttpContext.RequestServices
            .GetRequiredService<IUserProfileService>();

        // Merge anonymous profile if present
        Guid? anonId = null;
        if (context.HttpContext.Request.Cookies.TryGetValue("AnonUserId", out var anonCookie) && Guid.TryParse(anonCookie, out var parsedAnonId))
        {
            anonId = parsedAnonId;
        }

        var userId = await profileService.CreateOrUpdateFromGoogleAsync(new GoogleProfileDto
        {
            GoogleId = googleId,
            Email = email ?? string.Empty,
            Name = name ?? string.Empty,
            Picture = picture
        });

        // Remove anonymous cookie after merge
        if (anonId.HasValue)
        {
            context.HttpContext.Response.Cookies.Delete("AnonUserId");
        }

        // Set identity key for authenticated user
        context.HttpContext.Items["PortfolioIdentity"] = userId;
    }

    /// <summary>
    /// Sets <paramref name="statusCode"/> for <c>/api</c> requests and issues the normal redirect for
    /// everything else. Also used by the Google handler's authorization-endpoint redirect.
    /// </summary>
    internal static Task ApiAwareRedirect<TOptions>(RedirectContext<TOptions> context, int statusCode)
        where TOptions : AuthenticationSchemeOptions
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }
}
