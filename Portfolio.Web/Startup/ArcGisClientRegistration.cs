using Microsoft.Extensions.Http.Resilience;
using Polly;
using Portfolio.Common.Configuration;
using Portfolio.Services;
using Portfolio.Services.Abstractions;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;
using Portfolio.Web.BackgroundServices;

namespace Portfolio.Web.Startup;

/// <summary>Timeout, retry and circuit-breaker numbers for one ArcGIS resilience pipeline.</summary>
/// <remarks>
/// The four ArcGIS pipelines share one retry predicate and differ only in these numbers, so there are
/// two presets.
/// </remarks>
internal sealed record ArcGisResilienceProfile(
    TimeSpan Timeout,
    int MaxRetryAttempts,
    TimeSpan RetryDelay,
    TimeSpan SamplingDuration,
    double FailureRatio,
    int MinimumThroughput,
    TimeSpan BreakDuration)
{
    /// <summary>Feature queries: a longer timeout, fewer retries and a slower-reacting breaker.</summary>
    internal static ArcGisResilienceProfile Features { get; } = new(
        Timeout: TimeSpan.FromSeconds(25),
        MaxRetryAttempts: 2,
        RetryDelay: TimeSpan.FromMilliseconds(500),
        SamplingDuration: TimeSpan.FromSeconds(60),
        FailureRatio: 0.5,
        MinimumThroughput: 5,
        BreakDuration: TimeSpan.FromSeconds(30));

    /// <summary>Batch, reverse and address geocoding.</summary>
    internal static ArcGisResilienceProfile Geocoding { get; } = new(
        Timeout: TimeSpan.FromSeconds(8),
        MaxRetryAttempts: 3,
        RetryDelay: TimeSpan.FromMilliseconds(300),
        SamplingDuration: TimeSpan.FromSeconds(30),
        FailureRatio: 0.5,
        MinimumThroughput: 8,
        BreakDuration: TimeSpan.FromSeconds(15));
}

internal static class ArcGisClientRegistration
{
    internal static IServiceCollection AddPortfolioArcGisClients(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IArcGisService, ArcGisService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
        .AddArcGisResilience("arcgis-features", ArcGisResilienceProfile.Features);
        services.AddOptions<BatchGeocodingOptions>()
            .Bind(configuration.GetSection(BatchGeocodingOptions.SectionName))
            .ValidateDataAnnotations();
        services.AddOptions<ReverseGeocodingOptions>()
            .Bind(configuration.GetSection(ReverseGeocodingOptions.SectionName))
            .ValidateDataAnnotations();

        services.AddHttpClient<IBatchGeocodingService, BatchGeocodingService>()
            .AddArcGisResilience("arcgis-batch", ArcGisResilienceProfile.Geocoding);
        // Bounded job queue (8) drained by 2 workers (D-10); the queue is per process.
        services.AddSingleton<IBatchJobQueue>(_ => new BoundedBatchJobQueue());
        services.AddHostedService<BatchGeocodingWorker>();
        services.AddHttpClient<IReverseGeocodingService, ReverseGeocodingService>()
            .AddArcGisResilience("arcgis-reverse", ArcGisResilienceProfile.Geocoding);
        services.AddHttpClient<IAddressStandardizationService, AddressStandardizationService>()
            .AddArcGisResilience("arcgis-address", ArcGisResilienceProfile.Geocoding);
        return services;
    }

    /// <summary>Adds the timeout, retry and circuit-breaker pipeline.</summary>
    /// <remarks>
    /// The retry predicate is narrower than the library default (it leaves out 408, 429 and timeouts),
    /// so it is spelled out here.
    /// </remarks>
    internal static IHttpResiliencePipelineBuilder AddArcGisResilience(
        this IHttpClientBuilder builder, string pipelineName, ArcGisResilienceProfile profile)
    {
        return builder.AddResilienceHandler(pipelineName, pipeline =>
        {
            pipeline.AddTimeout(profile.Timeout);

            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = profile.MaxRetryAttempts,
                BackoffType      = DelayBackoffType.Exponential,
                UseJitter        = true,
                Delay            = profile.RetryDelay,
                ShouldHandle     = args => ValueTask.FromResult(
                    args.Outcome.Exception is HttpRequestException ||
                    args.Outcome.Result?.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
            });

            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration  = profile.SamplingDuration,
                FailureRatio      = profile.FailureRatio,
                MinimumThroughput = profile.MinimumThroughput,
                BreakDuration     = profile.BreakDuration
            });
        });
    }
}
