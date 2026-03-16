using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Portfolio.Common.Configuration;
using Portfolio.Common.DTOs;
using Portfolio.Repositories;
using Portfolio.Repositories.Interfaces;
using Portfolio.Repositories.Repositories;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;
using Portfolio.Web.Middleware;
using Scalar.AspNetCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
Console.WriteLine("EF Core Connection String: " + builder.Configuration.GetConnectionString("DefaultConnection"));
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
builder.Services.AddScoped<ISavedFeatureService, SavedFeatureService>();
builder.Services.AddScoped<UserProfileSeedService>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<ISavedSearchService, SavedSearchService>();
builder.Services.AddHttpClient<IArcGisService, ArcGisService>();        

// --------------------------
// Session
// --------------------------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
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

    options.AddPolicy("Admin", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("role", "admin"));
});

builder.Services.AddControllers();
// --------------------------
// Swagger / API Explorer
// --------------------------
builder.Services.AddEndpointsApiExplorer();

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
// Data Protection Keys Folder (for Docker / Render)
// --------------------------
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/app/DataProtection-Keys"))
    .SetApplicationName("PortfolioApp");

var app = builder.Build();

// --------------------------
// Middleware
// --------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<AnonymousUserMiddleware>();

// --------------------------
// Swagger UI
// --------------------------

// OpenAPI & Scalar UI in development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

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
