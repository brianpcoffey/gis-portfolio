using Portfolio.Web.Startup;

var builder = WebApplication.CreateBuilder(args);
builder.ConfigurePortfolioKestrel();

// Startup log: the Redis, Data Protection and migration lines are written through this logger.
// NOTE: never log the connection string — it carries the DB password and would leak into
// stdout/log aggregation. Redis is masked; the DB string must not be logged at all.
using var loggerFactory = LoggerFactory.Create(config => config.AddConsole());
var logger = loggerFactory.CreateLogger("Startup");

// Registration order is the order the old inline blocks ran in; keep it.
var services = builder.Services;
services.AddPortfolioCore();
services.AddPortfolioRepositories();
services.AddPortfolioServices();
services.AddPortfolioArcGisClients(builder.Configuration);
var redis = services.AddPortfolioRedis(builder.Configuration, logger);
services.AddPortfolioForwardedHeaders(builder.Configuration, logger);
services.AddPortfolioHsts();
services.AddPortfolioCaching(redis);
services.AddPortfolioSession(builder.Environment);
services.AddPortfolioAuthentication(builder.Configuration, builder.Environment);
services.AddPortfolioControllers();
services.AddPortfolioRateLimiting();
services.AddPortfolioHealthChecks();
services.AddPortfolioApiVersioning();
services.AddPortfolioDatabase(builder.Configuration);
services.AddPortfolioDataProtection(redis, builder.Environment, logger);
// Last, so the order above is unchanged. Registers nothing unless OTEL_EXPORTER_OTLP_ENDPOINT is set.
services.AddPortfolioObservability(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UsePortfolioPipeline();
app.MapPortfolioEndpoints();

if (StartupMigrations.Run(app, args, builder.Configuration, logger) is { } exitCode)
    return exitCode;

app.Run();
return 0;
