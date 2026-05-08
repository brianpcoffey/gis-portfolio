using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    public interface IRasterTerrainService
    {
        Task<RasterHillshadeResultDto> GenerateHillshadeAsync(
            RasterHillshadeRequestDto request,
            CancellationToken cancellationToken = default);

        Task<HeatmapResultDto> GenerateHeatmapAsync(
            HeatmapRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
