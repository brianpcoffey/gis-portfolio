using System.Threading.RateLimiting;
using Portfolio.Web.RateLimiting;

namespace Portfolio.Web.Startup;

internal static class RateLimitingRegistration
{
    // Partitioned per-client (IP) limiting. RemoteIpAddress is the real client here because
    // ForwardedHeaders runs first (AddPortfolioForwardedHeaders, UseForwardedHeaders). In production the
    // request arrives as client -> Cloudflare -> Render (10.x) -> local proxy -> app, and
    // appsettings.Production.json trusts those hops with ForwardLimit 3, so the middleware takes the
    // client from X-Forwarded-For (see ForwardedHeadersSetup). If a Cloudflare range is missing from that
    // list, the key for such a request is the Cloudflare edge address (shared, but never client-chosen).
    //   Global   — a generous per-IP ceiling (300/min) on every routed request (blocks blunt flooding).
    //   "compute" — a sliding 60/min budget for the native-compute endpoints. Viewshed runs on
    //               every cell click, so a 15/min budget would break the UI.
    //   "expensive" — a tight 15/min budget for endpoints that call the paid ArcGIS API or run native
    //                 compute, applied via [EnableRateLimiting("expensive")].
    //   Controllers marked [CpuBound] additionally pass through one process-wide concurrency limiter
    //   (after their per-IP window, so over-budget clients are rejected without queueing) so total CPU
    //   stays bounded even if a per-IP key were ever spoofed. The ArcGIS proxy
    //   controllers are I/O-bound and are deliberately not [CpuBound]. Queue overflow returns 429
    //   with Retry-After: 5.
    //   "csp"    — the Content-Security-Policy report sink (30/min).
    // This runs EARLY in the pipeline (before auth and the anonymous-profile middleware) so a flood
    // is rejected before it can trigger per-request profile creation/seeding. That early placement
    // means no authenticated identity is available yet, so partitioning is by IP. Trade-off: clients
    // sharing one egress IP (corporate NAT, mobile CGNAT) share a budget — hence the generous global
    // ceiling; tune per deployment if shared-IP 429s appear.
    internal static IServiceCollection AddPortfolioRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Global: one per-client window and nothing else. Concurrency is NOT chained here because the
            // middleware evaluates the global limiter before the endpoint policy, which would queue an
            // over-budget client in the shared slots before its own budget could reject it.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            options.OnRejected = RateLimitRejection.Apply;

            // "compute" and "expensive" check the client's own window first, then (for [CpuBound]
            // controllers only) the process-wide shared concurrency limiter.
            options.AddPolicy<string>(ComputeRateLimiterPolicy.Name, new ComputeRateLimiterPolicy(SharedCpuConcurrency.Shared));
            options.AddPolicy<string>(ExpensiveRateLimiterPolicy.Name, new ExpensiveRateLimiterPolicy(SharedCpuConcurrency.Shared));

            options.AddPolicy("csp", ctx =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });
        return services;
    }

    private static string ClientKey(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
