namespace Portfolio.Web.Middleware
{
    /// <summary>
    /// Emits security response headers on every response (including static files and
    /// error responses). Runs early in the pipeline so headers are present before any
    /// downstream middleware begins writing the response body.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
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
            // A full script-src/style-src policy requires first migrating inline event
            // handlers to delegated listeners (or nonces); left as a follow-up.
            headers["Content-Security-Policy"] =
                "frame-ancestors 'none'; base-uri 'self'; object-src 'none'";

            return _next(context);
        }
    }
}
