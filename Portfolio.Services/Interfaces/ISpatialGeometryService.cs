using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    public interface ISpatialGeometryService
    {
        Task<TriangulationResultDto> TriangulateAsync(
            GeometryPointSetDto request,
            CancellationToken cancellationToken = default);

        Task<PolygonOperationResultDto> ClipToBoundingBoxAsync(
            PolygonClipRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
