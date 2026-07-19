using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Performs point-in-polygon spatial joins, tagging points with the zone that contains them.
    /// </summary>
    public interface ISpatialOverlayService
    {
        /// <summary>Assigns each point to the first containing zone and rolls up per-zone counts.</summary>
        Task<SpatialJoinResultDto> SpatialJoinAsync(
            SpatialJoinRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
