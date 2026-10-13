using System.Security.Cryptography;

namespace Portfolio.Web.Middleware
{
    /// <summary>
    /// Emits security response headers on every response (including static files and
    /// error responses). Runs early in the pipeline so headers are present before any
    /// downstream middleware begins writing the response body.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        /// <summary><see cref="HttpContext.Items"/> key holding the per-request CSP nonce.</summary>
        public const string NonceItemKey = "csp-nonce";

        private const string PermissionsPolicy =
            "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;

            // Prevent MIME-type sniffing.
            headers["X-Content-Type-Options"] = "nosniff";
            // Clickjacking protection (legacy header, still honoured by older browsers).
            headers["X-Frame-Options"] = "DENY";
            // Limit referrer leakage to cross-origin destinations.
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Minimal, non-breaking CSP: only directives that do NOT restrict script/style
            // sources, so the ArcGIS CDN and the app's inline scripts/handlers keep working.
            //   frame-ancestors 'none' — modern clickjacking protection (mirrors X-Frame-Options)
            //   base-uri 'self'        — blocks <base> tag injection
            //   object-src 'none'      — blocks plugin/embedded-object vectors
            // The full script/style policy is emitted below in Report-Only mode; it replaces
            // this header only after the soak period (findings-security 2.7).
            headers["Content-Security-Policy"] =
                "frame-ancestors 'none'; base-uri 'self'; object-src 'none'";

            // Full script/style policy, Report-Only (findings-security 2.4). The nonce is stamped
            // onto every <script>/<style> by CspNonceTagHelper.
            var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            context.Items[NonceItemKey] = nonce;
            headers["Content-Security-Policy-Report-Only"] = BuildReportOnlyPolicy(nonce);
            headers["Reporting-Endpoints"] = "csp=\"/csp-report\"";
            headers["Permissions-Policy"] = PermissionsPolicy;
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";

            // API responses that did not choose their own caching (e.g. via [ResponseCache])
            // carry per-request or personal data: never store them.
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.OnStarting(static state =>
                {
                    var response = (HttpResponse)state;
                    if (!response.Headers.ContainsKey("Cache-Control"))
                    {
                        response.Headers["Cache-Control"] = "no-store";
                    }
                    return Task.CompletedTask;
                }, context.Response);
            }

            return _next(context);
        }

        private static string BuildReportOnlyPolicy(string nonce) =>
            "default-src 'self'; " +
            $"script-src 'self' 'nonce-{nonce}' https://js.arcgis.com https://unpkg.com/leaflet@1.9.4/; " +
            "script-src-attr 'none'; " +
            $"style-src 'self' 'nonce-{nonce}' https://js.arcgis.com https://unpkg.com/leaflet@1.9.4/; " +
            "style-src-attr 'unsafe-inline'; " +
            "img-src 'self' data: blob: https://*.arcgis.com https://*.arcgisonline.com https://tile.openstreetmap.org " +
            "https://*.tile.openstreetmap.org https://*.googleusercontent.com https://developers.google.com https://unpkg.com/leaflet@1.9.4/; " +
            "font-src 'self' data: https://js.arcgis.com https://*.arcgis.com; " +
            "connect-src 'self' https://*.arcgis.com https://*.arcgisonline.com https://tile.openstreetmap.org https://*.tile.openstreetmap.org; " +
            "worker-src 'self' blob:; child-src 'self' blob:; frame-src 'none'; object-src 'none'; " +
            "base-uri 'self'; form-action 'self'; manifest-src 'self'; frame-ancestors 'none'; " +
            "report-uri /csp-report; report-to csp";
    }
}
