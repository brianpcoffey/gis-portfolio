using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Multitemporal raster change detection: Change Vector Analysis, Otsu thresholding,
    /// morphological speckle removal, and connected-component detection extraction.
    /// </summary>
    public interface IChangeDetectionService
    {
        /// <summary>Builds the deterministic synthetic two-epoch scene, including its ground-truth changes.</summary>
        Task<ChangeSceneDto> GetSceneAsync(
            int width,
            int height,
            double noiseLevel,
            CancellationToken cancellationToken = default);

        /// <summary>Runs the full change detection pipeline over a co-registered two-epoch stack.</summary>
        Task<DetectResultDto> DetectAsync(
            DetectRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
