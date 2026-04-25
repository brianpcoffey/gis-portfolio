using Microsoft.AspNetCore.Http;
using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    public interface IBatchGeocodingService
    {
        // Parses the uploaded CSV file and geocodes each row using configurable concurrency.
        // Returns a list of results with match status and coordinates.
        Task<List<BatchGeocodingResultDto>> GeocodeAsync(IFormFile file, CancellationToken cancellationToken = default);
    }
}
