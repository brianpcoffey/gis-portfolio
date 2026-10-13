using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Portfolio.E2E.Infrastructure;

/// <summary>
/// Test-only sign-in for the E2E host. It exists ONLY in this project; production code never contains it
/// (Portfolio.Tests/Contracts/E2EIsolationTests enforces that).
/// </summary>
/// <remarks>
/// <para>
/// Google OAuth cannot run in a test, so a browser test visits <c>GET /__e2e/signin?returnUrl=/path</c>. The
/// middleware signs a principal in through the REAL cookie scheme, so <c>PortfolioCookieEvents.SignedIn</c>
/// runs exactly as it does after Google: the anonymous profile is adopted, the demo data is seeded, and the
/// auth cookie is issued and validated by the production handler. Each visit uses a fresh Google id
/// (<c>e2e-google-&lt;n&gt;</c>), i.e. a fresh user.
/// </para>
/// <para>
/// It is wired with <c>app.Use</c> and an exact path check, deliberately not <c>app.Map</c>: Map moves the
/// matched segment into <c>PathBase</c>, and the cookie middleware then issues the auth cookie with
/// <c>Path=/__e2e/signin</c>, so the browser would never send it to any other URL.
/// </para>
/// </remarks>
internal sealed class E2ESignIn : IStartupFilter
{
    /// <summary>The only path this middleware answers.</summary>
    public const string SignInPath = "/__e2e/signin";

    private static int _nextUser;

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, pipeline) =>
        {
            if (!HttpMethods.IsGet(context.Request.Method) ||
                !context.Request.Path.Equals(SignInPath, StringComparison.Ordinal))
            {
                await pipeline(context);
                return;
            }

            var user = Interlocked.Increment(ref _nextUser);
            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, $"e2e-google-{user}"),
                    new Claim(ClaimTypes.Email, "e2e@example.test"),
                    new Claim(ClaimTypes.Name, "E2E Visitor")
                ],
                authenticationType: "E2E");

            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            context.Response.Redirect(SafeReturnUrl(context.Request.Query["returnUrl"].ToString()));
        });

        next(app);
    };

    /// <summary>Only a local path is honoured; anything else (absolute URL, <c>//host</c>, <c>/\host</c>) becomes <c>/</c>.</summary>
    internal static string SafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl) ||
            returnUrl[0] != '/' ||
            returnUrl.StartsWith("//", StringComparison.Ordinal) ||
            returnUrl.StartsWith("/\\", StringComparison.Ordinal))
        {
            return "/";
        }

        return returnUrl;
    }
}
