using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Portfolio.Web.Middleware;
using Scalar.AspNetCore;

namespace Portfolio.Web.Startup;

internal static class Pipeline
{
    internal static WebApplication UsePortfolioPipeline(this WebApplication app)
    {
        // Apply X-Forwarded-For / X-Forwarded-Proto from Cloudflare and Render's TLS-terminating proxy.
        // Must be first so every downstream middleware sees the correct scheme and IP.
        // Options are registered by AddPortfolioForwardedHeaders; the hop layout is explained at ForwardedHeadersSetup.
        app.UseForwardedHeaders();

        // Emit security headers on every response (before static files / endpoints run).
        app.UseMiddleware<SecurityHeadersMiddleware>();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        // Only redirect to HTTPS in development; Render handles TLS externally and
        // the container only binds to HTTP, so redirecting here would produce a redirect loop.
        if (app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }
        // Versioned URLs (?v=..., from asp-append-version) change whenever the file does, so they can be
        // cached for a year; unversioned assets get a 1 hour cache. ETag/304 handling is unchanged.
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = static ctx =>
            {
                ctx.Context.Response.Headers.CacheControl =
                    ctx.Context.Request.Query.ContainsKey("v")
                        ? "public,max-age=31536000,immutable"
                        : "public,max-age=3600";
            }
        });
        app.UseRouting();

        // Rate limiting runs after routing (so per-endpoint policies resolve) but before the
        // endpoints. Static files (served above) are intentionally not rate-limited.
        app.UseRateLimiter();

        // Second CSRF layer: a state-changing /api request that carries an identity cookie needs X-Requested-With.
        // After the limiter so a rejected request still counts against the limits; before the session, the
        // anonymous-identity middleware and authentication so nothing downstream runs (no profile touch, no new cookie).
        app.UseMiddleware<ApiRequestHeaderMiddleware>();

        app.UseSession();

        // Global exception handler — returns RFC 7807 ProblemDetails for all unhandled exceptions.
        app.UseMiddleware<ApiExceptionMiddleware>();

        app.UseMiddleware<AnonymousUserMiddleware>();

        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    internal static WebApplication MapPortfolioEndpoints(this WebApplication app)
    {
        // OpenAPI & Scalar UI are intentionally available in production for public API documentation.
        app.MapOpenApi();
        // scalar-config.js adds X-Requested-With to every Try-it request, so a browser holding an identity cookie
        // can still send POST/PUT/DELETE from the reference (the header is required by ApiRequestHeaderMiddleware).
        app.MapScalarApiReference(options => options.WithJavaScriptConfiguration("/js/scalar-config.js"));

        // Health probes are anonymous. Liveness: process is up. Run NO checks so a transient DB blip
        // never restarts the pod. Probes hit these frequently from the kubelet, so they opt out of
        // rate limiting.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        }).DisableRateLimiting();
        // Readiness: only receive traffic once dependency checks tagged "ready" pass.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        }).DisableRateLimiting();

        app.MapControllers();
        app.MapRazorPages();
        return app;
    }
}
