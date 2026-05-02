using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Portfolio.Common.Models;
using Portfolio.Common.Serialization;
using Portfolio.Services.Abstractions;
using System.Text.Json;

namespace Portfolio.Services;

/// <summary>
/// <see cref="IBatchJobStore"/> implementation backed by Redis via <see cref="IDistributedCache"/>.
/// Each job is serialized to JSON on every write and deserialized on every read, enabling
/// any pod in the cluster to serve status polls for any job regardless of where it was submitted.
/// </summary>
public sealed class RedisBatchJobStore : IBatchJobStore
{
    private static readonly TimeSpan JobTtl = TimeSpan.FromHours(24);

    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisBatchJobStore> _logger;

    /// <summary>Initializes a new instance of <see cref="RedisBatchJobStore"/>.</summary>
    public RedisBatchJobStore(IDistributedCache cache, ILogger<RedisBatchJobStore> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    private static string Key(string jobId) => $"batchjob:{jobId}";

    /// <summary>Serializes <paramref name="job"/> and writes it to Redis with a 24-hour TTL.</summary>
    public async Task CreateAsync(BatchJob job, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(job, PortfolioJsonOptions.Default);
        await _cache.SetAsync(Key(job.JobId), bytes,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = JobTtl
            }, ct);
        _logger.LogInformation("Batch job {JobId} created in Redis.", job.JobId);
    }

    /// <summary>
    /// Returns the deserialized <see cref="BatchJob"/> for <paramref name="jobId"/>,
    /// or <c>null</c> if the key does not exist or has expired.
    /// </summary>
    public async Task<BatchJob?> GetAsync(string jobId, CancellationToken ct = default)
    {
        var bytes = await _cache.GetAsync(Key(jobId), ct);
        if (bytes is null)
            return null;
        return JsonSerializer.Deserialize<BatchJob>(bytes, PortfolioJsonOptions.Default);
    }

    /// <summary>
    /// Serializes the updated <paramref name="job"/> snapshot and overwrites the Redis entry,
    /// resetting the 24-hour TTL from the moment of the last update.
    /// </summary>
    public async Task UpdateAsync(BatchJob job, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(job, PortfolioJsonOptions.Default);
        await _cache.SetAsync(Key(job.JobId), bytes,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = JobTtl
            }, ct);
    }
}
