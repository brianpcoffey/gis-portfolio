using Portfolio.Common.Models;

namespace Portfolio.Services.Abstractions;

/// <summary>
/// Provides create/read/update operations for in-flight batch geocoding jobs.
/// </summary>
public interface IBatchJobStore
{
    /// <summary>Persists a newly created job record.</summary>
    Task CreateAsync(BatchJob job, CancellationToken ct = default);

    /// <summary>Returns the job with the given <paramref name="jobId"/>, or <c>null</c> if not found.</summary>
    Task<BatchJob?> GetAsync(string jobId, CancellationToken ct = default);

    /// <summary>Replaces the stored job record with the supplied updated instance.</summary>
    Task UpdateAsync(BatchJob job, CancellationToken ct = default);
}
