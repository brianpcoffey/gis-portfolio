using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Computes line-of-sight viewsheds (visible terrain) over dense elevation grids.
    /// </summary>
    public interface IViewshedService
    {
        /// <summary>Computes the set of cells visible from the requested observer position.</summary>
        Task<ViewshedResultDto> ComputeAsync(
            ViewshedRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
