using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Portfolio.Repositories.Diagnostics;

namespace Portfolio.Web.Middleware
{
    /// <summary>
    /// Second CSRF layer: a state-changing <c>/api</c> request that carries an identity cookie must also carry a
    /// non-empty <c>X-Requested-With</c> header.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first layer is <c>SameSite=Lax</c> on both identity cookies. This layer does not depend on the browser's
    /// cookie policy: a cross-site page cannot add a custom header to a request without a CORS preflight, and the API
    /// grants none, so a forged form post or <c>fetch</c> never carries it. The value is not checked (any non-empty
    /// value passes); only its presence proves the request came from script on an origin the browser let set it.
    /// </para>
    /// <para>
    /// The rule applies when all of these hold: the path is under <c>/api</c>; the method is not GET, HEAD or OPTIONS
    /// (everything else is treated as state-changing, so an unusual verb cannot slip past); and the request carries an
    /// identity cookie, either <c>AnonUserId</c> or the cookie-authentication cookie (matched by prefix, because a large
    /// ticket is split into <c>.AspNetCore.CookiesC1</c>, <c>C2</c>, ...). A request with no identity cookie has no
    /// ambient authority, so curl, probes and Scalar without cookies pass unchanged. <c>/csp-report</c> is not under
    /// <c>/api</c>: browsers send CSP reports themselves and cannot add headers to them.
    /// </para>
    /// <para>
    /// It runs after the rate limiter (a rejected request still counts against the limits) and before the session,
    /// the anonymous-identity middleware and authentication, so nothing downstream, including a profile write or a new
    /// <c>AnonUserId</c>, happens for a rejected request.
    /// </para>
    /// </remarks>
    public class ApiRequestHeaderMiddleware
    {
        /// <summary>The header every cookie-bearing, state-changing <c>/api</c> request must carry.</summary>
        public const string HeaderName = "X-Requested-With";

        /// <summary>The <c>error</c> text of the 403 body (the client reads <c>body.error</c>).</summary>
        public const string ErrorMessage = "This request must include the X-Requested-With header.";

        // Must stay equal to the cookie AnonymousUserMiddleware issues; the integration tests send the real one back.
        private const string AnonymousCookieName = "AnonUserId";

        // ".AspNetCore.Cookies": the name the cookie handler's post-configuration gives an unnamed cookie.
        private static readonly string DefaultAuthCookieName =
            CookieAuthenticationDefaults.CookiePrefix + CookieAuthenticationDefaults.AuthenticationScheme;

        private readonly RequestDelegate _next;
        private readonly IOptionsMonitor<CookieAuthenticationOptions> _cookieOptions;
        private readonly ILogger<ApiRequestHeaderMiddleware> _logger;

        public ApiRequestHeaderMiddleware(
            RequestDelegate next,
            IOptionsMonitor<CookieAuthenticationOptions> cookieOptions,
            ILogger<ApiRequestHeaderMiddleware> logger)
        {
            _next = next;
            _cookieOptions = cookieOptions;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;

            // Cheapest checks first; the cookie header is only parsed for an unsafe /api request without the header.
            if (!request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
                IsSafeMethod(request.Method) ||
                HasHeader(request) ||
                !HasIdentityCookie(request))
            {
                await _next(context);
                return;
            }

            _logger.LogWarning(
                "Rejected {Method} {Path}: identity cookie without X-Requested-With",
                LogRedaction.SingleLine(request.Method),
                LogRedaction.SingleLine(request.Path.Value));

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = ErrorMessage });
        }

        private static bool IsSafeMethod(string method) =>
            HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

        // Present with at least one non-blank value. Kestrel trims header values, so " " arrives as "".
        private static bool HasHeader(HttpRequest request) =>
            request.Headers.TryGetValue(HeaderName, out var values) && values.Any(v => !string.IsNullOrWhiteSpace(v));

        private bool HasIdentityCookie(HttpRequest request)
        {
            var cookies = request.Cookies;
            if (cookies.ContainsKey(AnonymousCookieName))
                return true;

            var authName = _cookieOptions.Get(CookieAuthenticationDefaults.AuthenticationScheme).Cookie.Name;
            if (string.IsNullOrEmpty(authName))
                authName = DefaultAuthCookieName;

            return cookies.Keys.Any(name => name.StartsWith(authName, StringComparison.Ordinal));
        }
    }
}
