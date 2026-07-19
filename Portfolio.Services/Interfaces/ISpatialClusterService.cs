using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Groups spatial points into density-based clusters (DBSCAN) and flags outliers as noise.
    /// </summary>
    public interface ISpatialClusterService
    {
        /// <summary>Clusters the supplied points using DBSCAN with the requested epsilon and minimum-points settings.</summary>
        Task<DbscanResultDto> RunDbscanAsync(
            DbscanRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
