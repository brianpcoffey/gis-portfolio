using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Portfolio.Common.Configuration;
using Portfolio.Common.DTOs;
using Portfolio.Repositories;
using Portfolio.Repositories.Interfaces;
using Portfolio.Repositories.Repositories;
using Portfolio.Services;
using Portfolio.Services.Abstractions;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;
using Portfolio.Web.Middleware;
using Portfolio.Web.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Web.RateLimiting;
using System.Threading.RateLimiting;
using Scalar.AspNetCore;
using System.Security.Claims;
using Portfolio.Web.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigurePortfolioKestrel();

// --------------------------
// Startup Diagnostics
// --------------------------
using var loggerFactory = LoggerFactory.Create(config => config.AddConsole());
var logger = loggerFactory.CreateLogger("Startup");
// NOTE: never log the connection string here — it carries the DB password and
// would leak into stdout/log aggregation. Redis is masked below; the DB string
// must not be logged at all.
builder.Services.AddPortfolioCore();

// --------------------------
// Dependency Injection
// --------------------------

// Repositories

builder.Services.AddPortfolioRepositories();

// Services

builder.Services.AddPortfolioServices();
builder.Services.AddPortfolioArcGisClients(builder.Configuration);

var redis = builder.Services.AddPortfolioRedis(builder.Configuration, logger);

builder.Services.AddPortfolioForwardedHeaders(builder.Configuration, logger);

builder.Services.AddPortfolioHsts();

builder.Services.AddPortfolioCaching(redis);
builder.Services.AddPortfolioSession(builder.Environment);

builder.Services.AddPortfolioAuthentication(builder.Configuration, builder.Environment);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy        = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition      = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// --------------------------
// Rate Limiting
// --------------------------
// Partitioned per-client (IP) limiting. RemoteIpAddress reflects X-Forwarded-For here because
// ForwardedHeaders is configured above, so this is the real client behind Render's proxy.
//   Global   — a generous per-IP ceiling (300/min) on every routed request (blocks blunt flooding).
//   "compute" — a sliding 60/min budget for the native-compute endpoints. Viewshed runs on
//               every cell click, so a 15/min budget would break the UI.
//   "expensive" — a tight 15/min budget for endpoints that call the paid ArcGIS API or run native
//                 compute, applied via [EnableRateLimiting("expensive")].
//   Controllers marked [CpuBound] additionally pass through one process-wide concurrency limiter
//   (after their per-IP window, so over-budget clients are rejected without queueing) so total CPU
//   stays bounded even if the per-IP key is spoofed (findings-security 4.4). The ArcGIS proxy
//   controllers are I/O-bound and are deliberately not [CpuBound]. Queue overflow returns 429
//   with Retry-After: 5.
//   "csp"    — the Content-Security-Policy report sink (30/min).
// This runs EARLY in the pipeline (before auth and the anonymous-profile middleware) so a flood
// is rejected before it can trigger per-request profile creation/seeding. That early placement
// means no authenticated identity is available yet, so partitioning is by IP. Trade-off: clients
// sharing one egress IP (corporate NAT, mobile CGNAT) share a budget — hence the generous global
// ceiling; tune per deployment if shared-IP 429s appear.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static string ClientKey(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    // Global: one per-client window and nothing else. Concurrency is NOT chained here because the
    // middleware evaluates the global limiter before the endpoint policy, which would queue an
    // over-budget client in the shared slots before its own budget could reject it.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 300,
            Window      = TimeSpan.FromMinutes(1),
            QueueLimit  = 0
        }));

    options.OnRejected = RateLimitRejection.Apply;

    // "compute" and "expensive" check the client's own window first, then (for [CpuBound]
    // controllers only) the process-wide shared concurrency limiter (findings-security 4.4).
    options.AddPolicy<string>(ComputeRateLimiterPolicy.Name, new ComputeRateLimiterPolicy(SharedCpuConcurrency.Shared));
    options.AddPolicy<string>(ExpensiveRateLimiterPolicy.Name, new ExpensiveRateLimiterPolicy(SharedCpuConcurrency.Shared));

    options.AddPolicy("csp", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window      = TimeSpan.FromMinutes(1),
            QueueLimit  = 0
        }));
});

builder.Services.AddPortfolioHealthChecks();

// --------------------------
// API Versioning
// --------------------------
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    // Require explicit URL-segment versions so missing or ambiguous API versions are detected.
    options.AssumeDefaultVersionWhenUnspecified = false;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// --------------------------
// Swagger / API Explorer
// --------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddPortfolioDatabase(builder.Configuration);

builder.Services.AddPortfolioDataProtection(redis, builder.Environment, logger);

var app = builder.Build();

// --------------------------
// Middleware
// --------------------------

// Apply X-Forwarded-For / X-Forwarded-Proto from Render's TLS-terminating proxy.
// Must be first so every downstream middleware sees the correct scheme and IP.
// Options (KnownNetworks/KnownProxies cleared) are registered in the services section above.
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
app.UseStaticFiles();
app.UseRouting();

// Rate limiting runs after routing (so per-endpoint policies resolve) but before the
// endpoints. Static files (served above) are intentionally not rate-limited.
app.UseRateLimiter();

app.UseSession();

// Global exception handler — returns RFC 7807 ProblemDetails for all unhandled exceptions.
app.UseMiddleware<ApiExceptionMiddleware>();

app.UseMiddleware<AnonymousUserMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// --------------------------
// OpenAPI / Scalar API reference
// --------------------------

// OpenAPI & Scalar UI are intentionally available in production for public API documentation.
app.MapOpenApi();
app.MapScalarApiReference();

// --------------------------
// Health check endpoints (probed by Kubernetes; anonymous)
// --------------------------
// Liveness: process is up. Run NO checks so a transient DB blip never restarts the pod.
// Probes hit these frequently from the kubelet, so they opt out of rate limiting.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
}).DisableRateLimiting();
// Readiness: only receive traffic once dependency checks tagged "ready" pass.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).DisableRateLimiting();

// --------------------------
// Map endpoints
// --------------------------
app.MapControllers();
app.MapRazorPages();

// --------------------------
// Apply Migrations Safely
// --------------------------
// Migrating on startup is the right default: it keeps a fresh clone and any host
// without a deploy step working with no extra configuration.
//
// It is the wrong thing in production on a free instance, though. The container
// spins down when idle, so every cold start pays a round trip to Neon to check the
// migration history before the app can answer its first request — and that request
// is the one a recruiter is waiting on. Two opt-in switches move that cost to
// deploy time:
//
//   dotnet Portfolio.Web.dll --migrate   migrate, then exit without starting the
//                                        web host (Render's Pre-Deploy Command)
//   RunMigrationsOnStartup=false         skip the startup pass entirely
//
// Set both together, or neither. Setting only the env var means nothing migrates.
var migrateOnly = args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);
var migrateOnStartup = builder.Configuration.GetValue("RunMigrationsOnStartup", true);

if (migrateOnly || migrateOnStartup)
{
    using var scope = app.Services.CreateScope();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        db.Database.Migrate();
        logger.LogInformation("Database migration successful.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration failed.");

        // A startup failure is logged and tolerated, as before — the app may still
        // serve pages that do not touch the database. A --migrate failure must not
        // be swallowed: Render aborts the deploy on a non-zero exit, which is
        // exactly the behaviour wanted when the schema could not be brought up.
        if (migrateOnly)
            return 1;
    }
}

if (migrateOnly)
{
    logger.LogInformation("Migration-only run complete; exiting without starting the web host.");
    return 0;
}

app.Run();
return 0;
