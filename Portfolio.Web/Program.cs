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
using System.Threading.RateLimiting;
using Scalar.AspNetCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// --------------------------
// Kestrel limits
// --------------------------
// 1 MB is the default request-body ceiling for every endpoint; the compute and upload actions
// that legitimately need more raise it per endpoint with [RequestSizeLimit] (a Kestrel-level
// limit, so only a live run enforces it, not TestServer). The Server response header is dropped
// so the stack is not advertised.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1_000_000;
    options.AddServerHeader = false;
});

// --------------------------
// Startup Diagnostics
// --------------------------
using var loggerFactory = LoggerFactory.Create(config => config.AddConsole());
var logger = loggerFactory.CreateLogger("Startup");
// NOTE: never log the connection string here — it carries the DB password and
// would leak into stdout/log aggregation. Redis is masked below; the DB string
// must not be logged at all.
// --------------------------
// Razor Pages & Services
// --------------------------
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);

// --------------------------
// Dependency Injection
// --------------------------

// Repositories

builder.Services.AddScoped<IFiberClientRepository, FiberClientRepository>();
builder.Services.AddScoped<IFiberOrderRepository, FiberOrderRepository>();
builder.Services.AddScoped<IFiberShipmentRepository, FiberShipmentRepository>();
builder.Services.AddScoped<IFiberMaterialRepository, FiberMaterialRepository>();
builder.Services.AddScoped<IFiberInventoryTransactionRepository, FiberInventoryTransactionRepository>();
builder.Services.AddScoped<ICollectionRepository, CollectionRepository>();
builder.Services.AddScoped<IPropertyRepository, PropertyRepository>();
builder.Services.AddScoped<ISavedFeatureRepository, SavedFeatureRepository>();
builder.Services.AddScoped<ISavedSearchRepository, SavedSearchRepository>();
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();

// Services

builder.Services.AddScoped<IFiberOrderService, FiberOrderService>();
builder.Services.AddScoped<IFiberShipmentService, FiberShipmentService>();
builder.Services.AddScoped<IFiberMaterialService, FiberMaterialService>();
builder.Services.AddScoped<IFiberDashboardService, FiberDashboardService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<IHomeScoringService, HomeScoringService>();
builder.Services.AddScoped<IGeoStreamProcessorService, GeoStreamProcessorService>();
builder.Services.AddScoped<ISpatialGeometryService, SpatialGeometryService>();
builder.Services.AddScoped<IRasterTerrainService, RasterTerrainService>();
builder.Services.AddScoped<ISpatialGraphService, SpatialGraphService>();
builder.Services.AddScoped<ISpatialClusterService, SpatialClusterService>();
builder.Services.AddScoped<IViewshedService, ViewshedService>();
builder.Services.AddScoped<ISpatialOverlayService, SpatialOverlayService>();
builder.Services.AddScoped<ICatRiskService, CatRiskService>();
builder.Services.AddScoped<IChangeDetectionService, ChangeDetectionService>();
builder.Services.AddScoped<IOutageTraceService, OutageTraceService>();
builder.Services.AddScoped<IResponseCoverageService, ResponseCoverageService>();
builder.Services.AddScoped<IFleetRoutingService, FleetRoutingService>();
builder.Services.AddScoped<ISavedFeatureService, SavedFeatureService>();
builder.Services.AddScoped<UserProfileSeedService>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<ISavedSearchService, SavedSearchService>();
builder.Services.AddHttpClient<IArcGisService, ArcGisService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
})
.SetHandlerLifetime(Timeout.InfiniteTimeSpan)
.AddResilienceHandler("arcgis-features", pipeline =>
{
    pipeline.AddTimeout(TimeSpan.FromSeconds(25));

    pipeline.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 2,
        BackoffType      = DelayBackoffType.Exponential,
        UseJitter        = true,
        Delay            = TimeSpan.FromMilliseconds(500),
        ShouldHandle     = args => ValueTask.FromResult(
            args.Outcome.Exception is HttpRequestException ||
            args.Outcome.Result?.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
    });

    pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        SamplingDuration  = TimeSpan.FromSeconds(60),
        FailureRatio      = 0.5,
        MinimumThroughput = 5,
        BreakDuration     = TimeSpan.FromSeconds(30)
    });
});
// Strongly-typed, validated geocoding configuration.
builder.Services.AddOptions<BatchGeocodingOptions>()
    .Bind(builder.Configuration.GetSection(BatchGeocodingOptions.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddOptions<ReverseGeocodingOptions>()
    .Bind(builder.Configuration.GetSection(ReverseGeocodingOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddScoped<IBatchGeocodingService, BatchGeocodingService>();
builder.Services.AddHttpClient<IBatchGeocodingService, BatchGeocodingService>()
    .AddResilienceHandler("arcgis-batch", pipeline =>
    {
        pipeline.AddTimeout(TimeSpan.FromSeconds(8));

        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType      = DelayBackoffType.Exponential,
            UseJitter        = true,
            Delay            = TimeSpan.FromMilliseconds(300),
            ShouldHandle     = args => ValueTask.FromResult(
                args.Outcome.Exception is HttpRequestException ||
                args.Outcome.Result?.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
        });

        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            SamplingDuration  = TimeSpan.FromSeconds(30),
            FailureRatio      = 0.5,
            MinimumThroughput = 8,
            BreakDuration     = TimeSpan.FromSeconds(15)
        });
    });
builder.Services.AddScoped<IReverseGeocodingService, ReverseGeocodingService>();
builder.Services.AddHttpClient<IReverseGeocodingService, ReverseGeocodingService>()
    .AddResilienceHandler("arcgis-reverse", pipeline =>
    {
        pipeline.AddTimeout(TimeSpan.FromSeconds(8));

        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType      = DelayBackoffType.Exponential,
            UseJitter        = true,
            Delay            = TimeSpan.FromMilliseconds(300),
            ShouldHandle     = args => ValueTask.FromResult(
                args.Outcome.Exception is HttpRequestException ||
                args.Outcome.Result?.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
        });

        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            SamplingDuration  = TimeSpan.FromSeconds(30),
            FailureRatio      = 0.5,
            MinimumThroughput = 8,
            BreakDuration     = TimeSpan.FromSeconds(15)
        });
    });
builder.Services.AddScoped<IAddressStandardizationService, AddressStandardizationService>();
builder.Services.AddHttpClient<IAddressStandardizationService, AddressStandardizationService>()
    .AddResilienceHandler("arcgis-address", pipeline =>
    {
        pipeline.AddTimeout(TimeSpan.FromSeconds(8));

        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType      = DelayBackoffType.Exponential,
            UseJitter        = true,
            Delay            = TimeSpan.FromMilliseconds(300),
            ShouldHandle     = args => ValueTask.FromResult(
                args.Outcome.Exception is HttpRequestException ||
                args.Outcome.Result?.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
        });

        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            SamplingDuration  = TimeSpan.FromSeconds(30),
            FailureRatio      = 0.5,
            MinimumThroughput = 8,
            BreakDuration     = TimeSpan.FromSeconds(15)
        });
    });

// --------------------------
// Redis Configuration & Job Store
// --------------------------
var redisConnectionString = Portfolio.Web.Infrastructure.ConnectionStrings.NormalizeRedis(
    builder.Configuration["Redis__ConnectionString"] 
    ?? builder.Configuration["Redis:ConnectionString"] 
    ?? string.Empty);

var isRedisEnabled = !string.IsNullOrWhiteSpace(redisConnectionString);

if (isRedisEnabled)
{
    logger.LogInformation("Redis ENABLED - Connection: {RedisConnection}", 
        Portfolio.Web.Infrastructure.ConnectionStrings.MaskRedisPassword(redisConnectionString));
    builder.Services.AddSingleton<IBatchJobStore, RedisBatchJobStore>();
}
else
{
    logger.LogWarning("Redis DISABLED - using in-memory fallback for caching and job store");
    builder.Services.AddSingleton<IBatchJobStore, InMemoryBatchJobStore>();
}

// --------------------------
// Forwarded Headers (Render / reverse-proxy TLS termination)
// --------------------------
// Render terminates TLS at its proxy and forwards plain HTTP to the container.
// Clearing KnownNetworks and KnownProxies trusts the X-Forwarded-* headers
// regardless of the proxy IP, making Request.IsHttps, HTTPS redirection,
// and secure cookie detection accurate inside the container.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// --------------------------
// HSTS (non-Development only; see UseHsts below)
// --------------------------
// One year, current host only. includeSubDomains and preload are deliberately off:
// preload is effectively irreversible, and subdomains are not all under this app's control.
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = false;
    options.Preload = false;
});

// --------------------------
// Caching & Session
// --------------------------
if (isRedisEnabled)
{
    // Production / staging: shared Redis cache
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName  = "portfolio:";
    });
}
else
{
    // Local dev fallback: in-process distributed cache
    builder.Services.AddDistributedMemoryCache();
}

// AddMemoryCache kept for compatibility with any remaining IMemoryCache consumers.
builder.Services.AddMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

// --------------------------
// Authentication & Authorization
// --------------------------
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    // Challenge with the cookie scheme, NOT Google. An [Authorize] page or controller
    // that denies an unauthenticated request must land on the app's own /Login page
    // (via the cookie LoginPath / OnRedirectToLogin handler below), which then offers
    // an explicit "Continue with Google" button. Making Google the default challenge
    // scheme instead sends users straight to accounts.google.com — bypassing /Login and
    // surfacing a raw Google error (e.g. redirect_uri_mismatch) — and leaves the cookie
    // OnRedirectToLogin API-vs-page handling as dead code. Google is still invoked
    // explicitly by the Login page handler (Challenge(props, "Google")).
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Login";
    options.LogoutPath = "/Logout";
    // In development allow SameAsRequest so cookies can be used over HTTP (localhost);
    // in production require Secure.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    options.Events = new CookieAuthenticationEvents
    {
        // -------------------------------------------------------
        // Return 401 for unauthenticated API requests instead of
        // a 302 redirect to the login page. This prevents fetch/XHR
        // calls from silently following a redirect to Google OAuth,
        // which causes CORS errors because accounts.google.com
        // does not include the caller's origin in its CORS headers.
        //
        // Non-API (Razor Pages) requests still get the normal 302
        // redirect so full-page navigation to Google OAuth works.
        // -------------------------------------------------------
        OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        },
        OnSignedIn = async context =>
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
                Picture = picture,
                AnonymousUserId = anonId // Pass anonId for merge logic and testability
            });

            // Remove anonymous cookie after merge
            if (anonId.HasValue)
            {
                context.HttpContext.Response.Cookies.Delete("AnonUserId");
            }

            // Set identity key for authenticated user
            context.HttpContext.Items["PortfolioIdentity"] = userId;
        }
    };
})
.AddGoogle(options =>
{
    var googleSettings = builder.Configuration.GetSection("Authentication:Google")
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

    // Google is the default challenge scheme, so an unauthenticated request to a
    // protected endpoint would otherwise 302 straight to accounts.google.com. For
    // API calls that produces a cross-origin redirect the browser's fetch cannot
    // follow (no CORS headers), surfacing as an opaque failure instead of a clean
    // 401. Return 401 for /api paths so the client can react (redirect to /Login);
    // full-page navigations still get the normal Google redirect.
    options.Events.OnRedirectToAuthorizationEndpoint = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Authenticated", policy =>
        policy.RequireAuthenticatedUser());
});

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
//   Global   — a generous ceiling on every routed request (blocks blunt flooding), chained with
//              a shared concurrency cap on the compute/expensive endpoints so total CPU stays
//              bounded even if the per-IP partition key is spoofed (findings-security 4.4).
//   "compute" — a sliding 60/min budget for the native-compute endpoints. Viewshed runs on
//               every cell click, so a 15/min budget would break the UI.
//   "expensive" — a tight budget for endpoints that call the paid ArcGIS API or run native
//                 compute, applied via [EnableRateLimiting("expensive")].
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

    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window      = TimeSpan.FromMinutes(1),
                QueueLimit  = 0
            })),
        PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            RateLimitKeys.ConcurrencyKey(ctx) is { } key
                ? RateLimitPartition.GetConcurrencyLimiter(key, _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = Math.Max(2, Environment.ProcessorCount),
                    QueueLimit  = 16
                })
                : RateLimitPartition.GetNoLimiter("none")));

    options.AddPolicy("compute", ctx =>
        RateLimitPartition.GetSlidingWindowLimiter(ClientKey(ctx), _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit       = 60,
            Window            = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueLimit        = 0
        }));

    options.AddPolicy("csp", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window      = TimeSpan.FromMinutes(1),
            QueueLimit  = 0
        }));

    options.AddPolicy("expensive", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 15,
            Window      = TimeSpan.FromMinutes(1),
            QueueLimit  = 0
        }));
});

// --------------------------
// Health Checks (Kubernetes probes)
// --------------------------
// Liveness (/health/live) runs no checks — it only proves the process is responsive.
// Readiness (/health/ready) runs checks tagged "ready" (DB connectivity) so a pod is
// kept out of the Service until its dependencies are up.
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadyHealthCheck>("database", tags: new[] { "ready" });

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

// --------------------------
// Database Context (PostgreSQL via Render / Supabase)
// --------------------------
var databaseUrl = builder.Configuration.GetConnectionString("DefaultConnection")
                  ?? Environment.GetEnvironmentVariable("DATABASE_URL")
                  ?? throw new InvalidOperationException("Database connection string not set.");

var npgsqlBuilder = Portfolio.Web.Infrastructure.ConnectionStrings.ParsePostgres(databaseUrl);

// Use the parsed/normalized connection string (handles postgres:// URI format)
builder.Services.AddDbContext<PortfolioDbContext>(options =>
    options.UseNpgsql(npgsqlBuilder.ConnectionString));

// --------------------------
// Data Protection Key Persistence
// --------------------------
if (isRedisEnabled)
{
    // Production: persist keys to Redis so all replicas share the same key ring
    try
    {
        var redis = ConnectionMultiplexer.Connect(redisConnectionString!);
        builder.Services.AddDataProtection()
            .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys")
            .SetApplicationName("portfolio");
        logger.LogInformation("Data Protection keys will be stored in Redis");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to connect to Redis for Data Protection keys. Falling back to filesystem.");
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo("/app/DataProtection-Keys"))
            .SetApplicationName("portfolio");
    }
}
else
{
    // Local dev: persist keys to filesystem
    var keysPath = builder.Environment.IsDevelopment() 
        ? Path.Combine(Directory.GetCurrentDirectory(), "DataProtection-Keys")
        : "/app/DataProtection-Keys";

    Directory.CreateDirectory(keysPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("portfolio");
    logger.LogInformation("Data Protection keys will be stored in filesystem: {KeysPath}", keysPath);
}

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

/// <summary>
/// Selects which requests share the global compute concurrency cap. An internal static method
/// (not an inline lambda) so it can be unit-tested.
/// </summary>
internal static class RateLimitKeys
{
    /// <summary>The shared partition key for compute/expensive endpoints.</summary>
    internal const string ComputeConcurrencyKey = "compute-concurrency";

    /// <summary>Returns the shared key for compute/expensive endpoints, or null for everything else.</summary>
    internal static string? ConcurrencyKey(HttpContext ctx)
    {
        var policy = ctx.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        return policy is "compute" or "expensive" ? ComputeConcurrencyKey : null;
    }
}
