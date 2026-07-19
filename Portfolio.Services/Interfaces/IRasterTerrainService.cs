using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Generates raster terrain visualizations such as hillshades and heatmaps.
    /// </summary>
    public interface IRasterTerrainService
    {
        /// <summary>Generates a hillshade raster from the supplied terrain request.</summary>
        Task<RasterHillshadeResultDto> GenerateHillshadeAsync(
            RasterHillshadeRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>Generates a density heatmap raster from the supplied request.</summary>
        Task<HeatmapResultDto> GenerateHeatmapAsync(
            HeatmapRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
