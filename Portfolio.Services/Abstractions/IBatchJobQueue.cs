using Portfolio.Common.Models;

namespace Portfolio.Services.Abstractions;

/// <summary>A queued batch geocoding job: the job record plus the uploaded CSV bytes.</summary>
/// <param name="Job">The job record, already persisted in the <see cref="IBatchJobStore"/>.</param>
/// <param name="CsvBytes">The raw uploaded CSV, read on the request thread.</param>
public sealed record BatchWorkItem(BatchJob Job, byte[] CsvBytes);

/// <summary>
/// Thrown when the batch job queue is at capacity. The controller maps it to 503 with
/// <c>Retry-After</c>.
/// </summary>
public sealed class BatchQueueFullException : Exception
{
    /// <summary>Initializes a new instance of <see cref="BatchQueueFullException"/>.</summary>
    public BatchQueueFullException()
        : base("The batch geocoding queue is full.")
    {
    }
}

/// <summary>
/// A bounded, in-process queue of batch geocoding jobs. Producers never wait: a full queue
/// rejects the job so callers can answer 503 instead of piling up unbounded background work.
/// </summary>
public interface IBatchJobQueue
{
    /// <summary>Maximum number of jobs that can wait in the queue.</summary>
    int Capacity { get; }

    /// <summary>Adds <paramref name="item"/> to the queue; returns <c>false</c> when the queue is full.</summary>
    bool TryEnqueue(BatchWorkItem item);

    /// <summary>Removes and returns the next queued job without waiting; <c>false</c> when the queue is empty.</summary>
    bool TryDequeue(out BatchWorkItem? item);

    /// <summary>Yields queued jobs as they arrive until <paramref name="ct"/> is cancelled.</summary>
    IAsyncEnumerable<BatchWorkItem> ReadAllAsync(CancellationToken ct);
}
