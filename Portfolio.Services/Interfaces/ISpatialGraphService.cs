using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    public interface ISpatialGraphService
    {
        Task<RoadGraphDto> GetRedlandsGraphAsync(CancellationToken cancellationToken = default);

        Task<RouteResultDto> FindShortestPathAsync(
            RouteRequestDto request,
            CancellationToken cancellationToken = default);

        Task<ServiceAreaResultDto> ComputeServiceAreaAsync(
            ServiceAreaRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
