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

builder.Services.AddPortfolioControllers();

builder.Services.AddPortfolioRateLimiting();

builder.Services.AddPortfolioHealthChecks();

builder.Services.AddPortfolioApiVersioning();

builder.Services.AddPortfolioDatabase(builder.Configuration);

builder.Services.AddPortfolioDataProtection(redis, builder.Environment, logger);

var app = builder.Build();

app.UsePortfolioPipeline();
app.MapPortfolioEndpoints();

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
