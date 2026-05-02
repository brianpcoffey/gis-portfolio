using System.Collections.Concurrent;
using Portfolio.Common.Models;
using Portfolio.Services.Abstractions;

namespace Portfolio.Services;

/// <summary>
/// Thread-safe, in-process job store backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Jobs are held in memory for the lifetime of the application process.
/// </summary>
public sealed class InMemoryBatchJobStore : IBatchJobStore
{
    private readonly ConcurrentDictionary<string, BatchJob> _store = new();

    /// <inheritdoc/>
    public Task CreateAsync(BatchJob job, CancellationToken ct = default)
    {
        _store[job.JobId] = job;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<BatchJob?> GetAsync(string jobId, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(jobId, out var job) ? job : null);

    /// <inheritdoc/>
    public Task UpdateAsync(BatchJob job, CancellationToken ct = default)
    {
        _store[job.JobId] = job;
        return Task.CompletedTask;
    }
}
