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

if (StartupMigrations.Run(app, args, builder.Configuration, logger) is { } exitCode)
    return exitCode;

app.Run();
return 0;
