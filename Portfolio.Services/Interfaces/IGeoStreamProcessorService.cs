using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Processes batches of streamed geospatial features.
    /// </summary>
    public interface IGeoStreamProcessorService
    {
        /// <summary>Processes a batch of streamed geospatial features and returns the aggregated result.</summary>
        Task<GeoStreamBatchResultDto> ProcessBatchAsync(
            GeoStreamBatchRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
