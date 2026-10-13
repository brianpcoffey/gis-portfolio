using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Portfolio.Services;
using Portfolio.Services.Abstractions;
using Portfolio.Web.Startup;

namespace Portfolio.Tests.Startup;

/// <summary>Pins the option values each <c>AddPortfolio*</c> extension registers.</summary>
public class RegistrationOptionsTests
{
    private static IWebHostEnvironment Env(string name)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(name);
        return env.Object;
    }

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void AddPortfolioHsts_UsesOneYearWithoutSubdomainsOrPreload()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPortfolioHsts();
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<HstsOptions>>().Value;

        // Assert
        Assert.Equal(TimeSpan.FromDays(365), options.MaxAge);
        Assert.False(options.IncludeSubDomains);
        Assert.False(options.Preload);
    }

    [Theory]
    [InlineData("Development", CookieSecurePolicy.SameAsRequest)]
    [InlineData("Production", CookieSecurePolicy.Always)]
    public void AddPortfolioSession_SecurePolicyFollowsEnvironment(string environment, CookieSecurePolicy expected)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPortfolioSession(Env(environment));
        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Builder.SessionOptions>>().Value;

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(30), options.IdleTimeout);
        Assert.True(options.Cookie.HttpOnly);
        Assert.True(options.Cookie.IsEssential);
        Assert.Equal(expected, options.Cookie.SecurePolicy);
    }

    [Fact]
    public void AddPortfolioRedis_WithoutConnectionString_IsDisabledAndUsesInMemoryJobStore()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var redis = services.AddPortfolioRedis(Config(), NullLogger.Instance);

        // Assert
        Assert.False(redis.Enabled);
        Assert.Equal(string.Empty, redis.ConnectionString);
        var store = Assert.Single(services, d => d.ServiceType == typeof(IBatchJobStore));
        Assert.Equal(typeof(InMemoryBatchJobStore), store.ImplementationType);
    }

    [Fact]
    public void AddPortfolioRedis_WithConnectionString_IsEnabledAndUsesRedisJobStore()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var redis = services.AddPortfolioRedis(Config(("Redis:ConnectionString", "localhost:6379")), NullLogger.Instance);

        // Assert
        Assert.True(redis.Enabled);
        Assert.Contains("localhost:6379", redis.ConnectionString);
        var store = Assert.Single(services, d => d.ServiceType == typeof(IBatchJobStore));
        Assert.Equal(typeof(RedisBatchJobStore), store.ImplementationType);
    }

    [Fact]
    public void AddPortfolioCaching_WithRedisDisabled_UsesInProcessDistributedCache()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPortfolioCaching(new RedisSettings(false, string.Empty));
        var cache = services.BuildServiceProvider().GetRequiredService<IDistributedCache>();

        // Assert
        Assert.IsType<MemoryDistributedCache>(cache);
    }

    [Fact]
    public void AddPortfolioControllers_ConfiguresCamelCaseNullSkippingEnumStrings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddPortfolioControllers();
        var json = services.BuildServiceProvider()
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;

        // Assert
        Assert.Equal(System.Text.Json.JsonNamingPolicy.CamelCase, json.PropertyNamingPolicy);
        Assert.Equal(System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull, json.DefaultIgnoreCondition);
        Assert.Contains(json.Converters, c => c is System.Text.Json.Serialization.JsonStringEnumConverter);
    }

    [Fact]
    public void AddPortfolioApiVersioning_RequiresExplicitUrlSegmentVersions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();

        // Act
        services.AddPortfolioApiVersioning();
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ApiVersioningOptions>>().Value;

        // Assert
        Assert.Equal(new ApiVersion(1, 0), options.DefaultApiVersion);
        Assert.False(options.AssumeDefaultVersionWhenUnspecified);
        Assert.True(options.ReportApiVersions);
        Assert.IsType<UrlSegmentApiVersionReader>(options.ApiVersionReader);
    }

    [Fact]
    public void AddPortfolioRateLimiting_Rejects_With429()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPortfolioRateLimiting();
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        // Assert
        Assert.Equal(StatusCodes.Status429TooManyRequests, options.RejectionStatusCode);
        Assert.NotNull(options.GlobalLimiter);
        Assert.NotNull(options.OnRejected);
    }

    [Fact]
    public void ArcGisResilienceProfiles_PinTheTwoPresets()
    {
        // Arrange / Act
        var features = ArcGisResilienceProfile.Features;
        var geocoding = ArcGisResilienceProfile.Geocoding;

        // Assert: feature queries retry twice (3 attempts), geocoding three times (4 attempts).
        Assert.Equal(TimeSpan.FromSeconds(25), features.Timeout);
        Assert.Equal(2, features.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromSeconds(30), features.BreakDuration);
        Assert.Equal(5, features.MinimumThroughput);
        Assert.Equal(TimeSpan.FromSeconds(8), geocoding.Timeout);
        Assert.Equal(3, geocoding.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromSeconds(15), geocoding.BreakDuration);
        Assert.Equal(8, geocoding.MinimumThroughput);
    }
}
