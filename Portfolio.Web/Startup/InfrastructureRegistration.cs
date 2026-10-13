using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Portfolio.Services;
using Portfolio.Services.Abstractions;
using Portfolio.Web.HealthChecks;
using StackExchange.Redis;

namespace Portfolio.Web.Startup;

/// <summary>Whether Redis is configured, and the normalized connection string (empty when it is not).</summary>
internal sealed record RedisSettings(bool Enabled, string ConnectionString);

internal static class InfrastructureRegistration
{
    // --------------------------
    // Redis Configuration & Job Store
    // --------------------------
    internal static RedisSettings AddPortfolioRedis(this IServiceCollection services, IConfiguration configuration, ILogger logger)
    {
        var redisConnectionString = Portfolio.Web.Infrastructure.ConnectionStrings.NormalizeRedis(
            configuration["Redis__ConnectionString"] 
            ?? configuration["Redis:ConnectionString"] 
            ?? string.Empty);

        var isRedisEnabled = !string.IsNullOrWhiteSpace(redisConnectionString);

        if (isRedisEnabled)
        {
            logger.LogInformation("Redis ENABLED - Connection: {RedisConnection}", 
                Portfolio.Web.Infrastructure.ConnectionStrings.MaskRedisPassword(redisConnectionString));
            services.AddSingleton<IBatchJobStore, RedisBatchJobStore>();
        }
        else
        {
            logger.LogWarning("Redis DISABLED - using in-memory fallback for caching and job store");
            services.AddSingleton<IBatchJobStore, InMemoryBatchJobStore>();
        }

        return new RedisSettings(isRedisEnabled, redisConnectionString);
    }

    // --------------------------
    // Forwarded Headers (Render / reverse-proxy TLS termination)
    // --------------------------
    // Render terminates TLS at its proxy and forwards plain HTTP to the container.
    // With no ForwardedHeaders:KnownNetworks configured (the default), KnownNetworks and
    // KnownProxies are cleared, which trusts the X-Forwarded-* headers regardless of the proxy IP,
    // making Request.IsHttps, HTTPS redirection, and secure cookie detection accurate inside the
    // container. Once the owner has verified Render's proxy range (findings-security 4.5), set
    // ForwardedHeaders:KnownNetworks (CIDRs) and, if a further proxy hop is confirmed,
    // ForwardedHeaders:ForwardLimit. Never raise ForwardLimit without KnownNetworks.
    internal static IServiceCollection AddPortfolioForwardedHeaders(this IServiceCollection services, IConfiguration configuration, ILogger logger)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
            ForwardedHeadersSetup.Apply(options, configuration, logger));
        return services;
    }

    // --------------------------
    // HSTS (non-Development only; see UseHsts below)
    // --------------------------
    // One year, current host only. includeSubDomains and preload are deliberately off:
    // preload is effectively irreversible, and subdomains are not all under this app's control.
    internal static IServiceCollection AddPortfolioHsts(this IServiceCollection services)
    {
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = false;
            options.Preload = false;
        });
        return services;
    }

    // --------------------------
    // Caching & Session
    // --------------------------
    internal static IServiceCollection AddPortfolioCaching(this IServiceCollection services, RedisSettings redisSettings)
    {
        var (isRedisEnabled, redisConnectionString) = redisSettings;
        if (isRedisEnabled)
        {
            // Production / staging: shared Redis cache
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName  = "portfolio:";
            });
        }
        else
        {
            // Local dev fallback: in-process distributed cache
            services.AddDistributedMemoryCache();
        }
        return services;
    }

    internal static IServiceCollection AddPortfolioSession(this IServiceCollection services, IWebHostEnvironment environment)
    {
        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });
        return services;
    }

    // --------------------------
    // Health Checks (Kubernetes probes)
    // --------------------------
    // Liveness (/health/live) runs no checks — it only proves the process is responsive.
    // Readiness (/health/ready) runs checks tagged "ready" (DB connectivity) so a pod is
    // kept out of the Service until its dependencies are up.
    internal static IServiceCollection AddPortfolioHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseReadyHealthCheck>("database", tags: new[] { "ready" });
        return services;
    }

    // --------------------------
    // Data Protection Key Persistence
    // --------------------------
    internal static IServiceCollection AddPortfolioDataProtection(this IServiceCollection services, RedisSettings redisSettings, IWebHostEnvironment environment, ILogger logger)
    {
        var (isRedisEnabled, redisConnectionString) = redisSettings;
        if (isRedisEnabled)
        {
            // Production: persist keys to Redis so all replicas share the same key ring
            try
            {
                var redis = ConnectionMultiplexer.Connect(redisConnectionString!);
                services.AddDataProtection()
                    .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys")
                    .SetApplicationName("portfolio");
                logger.LogInformation("Data Protection keys will be stored in Redis");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to Redis for Data Protection keys. Falling back to filesystem.");
                services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo("/app/DataProtection-Keys"))
                    .SetApplicationName("portfolio");
            }
        }
        else
        {
            // Local dev: persist keys to filesystem
            var keysPath = environment.IsDevelopment() 
                ? Path.Combine(Directory.GetCurrentDirectory(), "DataProtection-Keys")
                : "/app/DataProtection-Keys";

            Directory.CreateDirectory(keysPath);
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
                .SetApplicationName("portfolio");
            logger.LogInformation("Data Protection keys will be stored in filesystem: {KeysPath}", keysPath);
        }
        return services;
    }
}
