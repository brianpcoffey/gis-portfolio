using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    public interface IGeoStreamProcessorService
    {
        Task<GeoStreamBatchResultDto> ProcessBatchAsync(
            GeoStreamBatchRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
