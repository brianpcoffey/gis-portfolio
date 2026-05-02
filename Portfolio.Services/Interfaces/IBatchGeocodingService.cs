using Microsoft.AspNetCore.Http;
using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    public interface IBatchGeocodingService
    {
        // Parses the uploaded CSV file and geocodes each row using configurable concurrency.
        // Returns a list of results with match status and coordinates.
        Task<List<BatchGeocodingResultDto>> GeocodeAsync(IFormFile file, CancellationToken cancellationToken = default);

        // Enqueues a CSV file for background geocoding and returns a job ID immediately.
        // The caller polls GET /geocoding/batch/{jobId}/status for progress and results.
        Task<string> EnqueueAsync(IFormFile file, CancellationToken ct = default);
    }
}
