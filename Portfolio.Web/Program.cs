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
using Scalar.AspNetCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// --------------------------
// Startup Diagnostics
// --------------------------
var logger = LoggerFactory.Create(config => config.AddConsole()).CreateLogger("Startup");
logger.LogInformation("EF Core Connection String: {ConnectionString}", 
    builder.Configuration.GetConnectionString("DefaultConnection"));
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
var redisConnectionString = NormalizeRedisConnectionString(
    builder.Configuration["Redis__ConnectionString"] 
    ?? builder.Configuration["Redis:ConnectionString"] 
    ?? string.Empty);

var isRedisEnabled = !string.IsNullOrWhiteSpace(redisConnectionString);

if (isRedisEnabled)
{
    logger.LogInformation("Redis ENABLED - Connection: {RedisConnection}", 
        MaskRedisPassword(redisConnectionString));
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
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
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
    options.SaveTokens = true;
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.ClaimActions.MapJsonKey("picture", "picture");
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Authenticated", policy =>
        policy.RequireAuthenticatedUser());
});

builder.Services.AddControllers();

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

var npgsqlBuilder = ParsePostgresConnectionString(databaseUrl);

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
app.UseSession();

// Global exception handler — returns RFC 7807 ProblemDetails for all unhandled exceptions.
app.UseMiddleware<ApiExceptionMiddleware>();

app.UseMiddleware<AnonymousUserMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// --------------------------
// Swagger UI
// --------------------------

// OpenAPI & Scalar UI are intentionally available in production for public API documentation.
app.MapOpenApi();
app.MapScalarApiReference();

// --------------------------
// Map endpoints
// --------------------------
app.MapControllers();
app.MapRazorPages();

// --------------------------
// Apply Migrations Safely
// --------------------------
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        db.Database.Migrate();
        Console.WriteLine("Database migration successful.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database migration failed: {ex.Message}");
    }
}

app.Run();

// --------------------------
// Helper: Parse postgres:// URI or standard connection string
// --------------------------
static NpgsqlConnectionStringBuilder ParsePostgresConnectionString(string connectionString)
{
    // If it's already a standard connection string (contains "Host="), use it directly
    if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
    {
        return new NpgsqlConnectionStringBuilder(connectionString);
    }

    // Parse URI format: postgres://user:pass@host:port/db or postgresql://user:pass@host/db
    if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
    {
        throw new ArgumentException($"Invalid connection string format: {connectionString}");
    }

    if (uri.Scheme is not ("postgres" or "postgresql"))
    {
        throw new ArgumentException($"Unsupported URI scheme: {uri.Scheme}. Expected 'postgres' or 'postgresql'.");
    }

    // Parse user info (user:password)
    var userInfo = uri.UserInfo.Split(':', 2);
    if (userInfo.Length < 2 || string.IsNullOrEmpty(userInfo[0]))
    {
        throw new ArgumentException("Connection string must include username and password.");
    }

    // URL-decode username and password (handles special characters like @ or %)
    var username = Uri.UnescapeDataString(userInfo[0]);
    var password = Uri.UnescapeDataString(userInfo[1]);

    // Default port to 5432 if not specified (uri.Port returns -1 when missing)
    var port = uri.Port > 0 ? uri.Port : 5432;

    // Handle IPv6 localhost
    var host = uri.Host;
    if (host == "[::1]")
    {
        host = "127.0.0.1";
    }

    // Extract database name from path
    var database = uri.AbsolutePath.TrimStart('/');
    if (string.IsNullOrEmpty(database))
    {
        throw new ArgumentException("Database name is required in the connection string.");
    }

    // Parse query string for additional options (e.g., ?sslmode=require)
    var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);

    var npgsqlBuilder = new NpgsqlConnectionStringBuilder
    {
        Host = host,
        Port = port,
        Username = username,
        Password = password,
        Database = database,
        SslMode = SslMode.Require
    };

    // Override SSL mode if specified in query string
    if (queryParams["sslmode"] is string sslModeValue &&
        Enum.TryParse<SslMode>(sslModeValue, ignoreCase: true, out var sslMode))
    {
        npgsqlBuilder.SslMode = sslMode;
    }

    return npgsqlBuilder;
}

// --------------------------
// Helper: Normalize Redis connection string
// --------------------------
static string NormalizeRedisConnectionString(string connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return string.Empty;

    connectionString = connectionString.Trim();

    // If it's already in StackExchange.Redis format (contains comma-separated options), return as-is
    if (!connectionString.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) &&
        !connectionString.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
    {
        return connectionString;
    }

    // Parse rediss:// or redis:// URI format (Upstash/Redis Cloud style)
    if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
    {
        throw new ArgumentException($"Invalid Redis connection string format: {connectionString}");
    }

    var useSsl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase);
    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : (useSsl ? 6380 : 6379);
    var password = string.IsNullOrEmpty(uri.UserInfo) ? null : Uri.UnescapeDataString(uri.UserInfo.Split(':').Last());

    // Build StackExchange.Redis-compatible connection string
    var configOptions = new List<string>
    {
        $"{host}:{port}"
    };

    if (!string.IsNullOrEmpty(password))
    {
        configOptions.Add($"password={password}");
    }

    if (useSsl)
    {
        configOptions.Add("ssl=True");
    }

    configOptions.Add("abortConnect=False");

    return string.Join(",", configOptions);
}

// --------------------------
// Helper: Mask Redis password for logging
// --------------------------
static string MaskRedisPassword(string connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return string.Empty;

    // Mask password= portion
    var parts = connectionString.Split(',');
    var masked = parts.Select(part =>
    {
        if (part.Trim().StartsWith("password=", StringComparison.OrdinalIgnoreCase))
            return "password=***";
        return part;
    });

    return string.Join(",", masked);
}

