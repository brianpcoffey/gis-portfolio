using System.Collections.Concurrent;
using Portfolio.Common.Models;
using Portfolio.Services.Abstractions;

namespace Portfolio.Services;

/// <summary>
/// Thread-safe in-process job store over a <see cref="ConcurrentDictionary{TKey,TValue}"/>; like the Redis store, a
/// job expires <see cref="JobTtl"/> after its last write, and expired jobs are pruned on writes and never returned
/// by reads.
/// </summary>
public sealed class InMemoryBatchJobStore : IBatchJobStore
{
    /// <summary>How long a job is kept after its last write, matching the Redis store's key expiry.</summary>
    public static readonly TimeSpan JobTtl = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<string, Entry> _store = new();
    private readonly TimeProvider _time;

    private sealed record Entry(BatchJob Job, DateTimeOffset WrittenAt);

    /// <summary>Initializes a new instance of <see cref="InMemoryBatchJobStore"/>.</summary>
    public InMemoryBatchJobStore(TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public Task CreateAsync(BatchJob job, CancellationToken ct = default)
    {
        Write(job);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<BatchJob?> GetAsync(string jobId, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(jobId, out var entry) && !IsExpired(entry, _time.GetUtcNow())
            ? entry.Job
            : null);

    /// <inheritdoc/>
    public Task UpdateAsync(BatchJob job, CancellationToken ct = default)
    {
        Write(job);
        return Task.CompletedTask;
    }

    private void Write(BatchJob job)
    {
        var now = _time.GetUtcNow();
        _store[job.JobId] = new Entry(job, now);
        foreach (var pair in _store)
        {
            if (IsExpired(pair.Value, now))
                _store.TryRemove(pair);
        }
    }

    private static bool IsExpired(Entry entry, DateTimeOffset now) => now - entry.WrittenAt >= JobTtl;
}
