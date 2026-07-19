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
        /// Enqueues a CSV file for background geocoding and returns immediately.
        /// The caller polls <c>GET /geocoding/batch/{jobId}/status</c> for progress and results.
        /// </summary>
        /// <returns>The job id used to poll for status.</returns>
        Task<string> EnqueueAsync(IFormFile file, CancellationToken ct = default);
    }
}
