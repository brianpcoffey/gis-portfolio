using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Startup;

/// <summary>
/// Cookie-authentication events: API-aware redirects, the post-sign-in profile merge, and the
/// 24 h absolute session lifetime.
/// </summary>
internal sealed class PortfolioCookieEvents : CookieAuthenticationEvents
{
    /// <summary>Claim holding the UTC sign-in instant as Unix seconds (decimal string).</summary>
    internal const string AuthTimeClaimType = "auth_time";

    /// <summary>Absolute cap on a cookie session, independent of the 8 h sliding window.</summary>
    internal static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(24);

    private readonly TimeProvider _time;

    public PortfolioCookieEvents() : this(TimeProvider.System)
    {
    }

    public PortfolioCookieEvents(TimeProvider time)
    {
        _time = time;
    }

    // Stamp the sign-in instant into the principal before the cookie ticket is
    // serialized. Sliding renewals re-issue the same ticket without raising
    // SigningIn, so the claim keeps the ORIGINAL sign-in time.
    public override Task SigningIn(CookieSigningInContext context)
    {
        if (context.Principal?.Identity is ClaimsIdentity identity)
        {
            foreach (var existing in identity.FindAll(AuthTimeClaimType).ToList())
                identity.RemoveClaim(existing);

            identity.AddClaim(new Claim(
                AuthTimeClaimType,
                _time.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64));
        }

        return base.SigningIn(context);
    }

    // Enforce the absolute lifetime. Cookies issued before auth_time existed
    // have no claim; they are not killed by the deploy, but are stamped with
    // the current time and renewed, so the 24 h cap starts at their first
    // validation instead of sliding forever. A present but unparseable claim
    // is rejected.
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (context.Principal is null)
        {
            await base.ValidatePrincipal(context);
            return;
        }

        var raw = context.Principal.FindFirstValue(AuthTimeClaimType);
        if (raw is null)
        {
            var stamped = new ClaimsPrincipal(context.Principal.Identities.Select(i => i.Clone()));
            stamped.Identities.First().AddClaim(new Claim(
                AuthTimeClaimType,
                _time.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64));
            context.ReplacePrincipal(stamped);
            context.ShouldRenew = true;
            await base.ValidatePrincipal(context);
            return;
        }

        if (long.TryParse(raw, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            && _time.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(seconds) <= AbsoluteLifetime)
        {
            await base.ValidatePrincipal(context);
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }

    // Return 401/403 for API requests instead of a 302 to the /Login
    // page, so fetch/XHR callers get a status they can react to
    // (the client then sends the user to /Login) instead of silently
    // following the redirect and parsing the login page as JSON.
    //
    // Non-API (Razor Pages) requests still get the normal 302 redirect.
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

        if (anonId.HasValue)
        {
            context.HttpContext.Response.Cookies.Delete("AnonUserId");
        }

        context.HttpContext.Items["PortfolioIdentity"] = userId;
    }

    /// <summary>
    /// Sets <paramref name="statusCode"/> for <c>/api</c> requests and issues the normal redirect for
    /// everything else (also used by the Google handler's authorization-endpoint redirect).
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
