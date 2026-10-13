using Microsoft.AspNetCore.Http;
using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Geocodes addresses in bulk from uploaded CSV files, either synchronously or as a background job.
    /// </summary>
    public interface IBatchGeocodingService
    {
        /// <summary>
        /// Parses the uploaded CSV file and geocodes each row using configurable concurrency.
        /// </summary>
        /// <returns>A list of results, each with match status and coordinates.</returns>
        Task<List<BatchGeocodingResultDto>> GeocodeAsync(IFormFile file, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a CSV file and queues a job, returning immediately; callers poll
        /// <c>GET /geocoding/batch/{jobId}/status</c> for progress and results.
        /// </summary>
        /// <returns>The job id used to poll for status.</returns>
        /// <exception cref="ArgumentException">The CSV exceeds the row cap.</exception>
        /// <exception cref="Portfolio.Services.Abstractions.BatchQueueFullException">The queue is at capacity.</exception>
        Task<string> EnqueueAsync(IFormFile file, CancellationToken ct = default);

        /// <summary>Runs a queued job to a terminal status (Completed or Failed); called by the hosted worker.</summary>
        Task ProcessJobAsync(Portfolio.Services.Abstractions.BatchWorkItem item, CancellationToken ct);
    }
}
