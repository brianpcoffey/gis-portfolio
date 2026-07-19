using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Performs computational geometry operations such as triangulation and polygon clipping.
    /// </summary>
    public interface ISpatialGeometryService
    {
        /// <summary>Triangulates the supplied point set and returns the resulting mesh.</summary>
        Task<TriangulationResultDto> TriangulateAsync(
            GeometryPointSetDto request,
            CancellationToken cancellationToken = default);

        /// <summary>Clips the supplied polygon to a bounding box and returns the resulting geometry.</summary>
        Task<PolygonOperationResultDto> ClipToBoundingBoxAsync(
            PolygonClipRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
