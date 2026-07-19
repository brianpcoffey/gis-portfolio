using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Performs Dijkstra/A* routing and service-area analysis over the Redlands OSM road graph.
    /// </summary>
    public interface ISpatialGraphService
    {
        /// <summary>Returns the Redlands road network as a graph of nodes and edges.</summary>
        Task<RoadGraphDto> GetRedlandsGraphAsync(CancellationToken cancellationToken = default);

        /// <summary>Computes the shortest path between two points over the road graph.</summary>
        Task<RouteResultDto> FindShortestPathAsync(
            RouteRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>Computes the reachable service area from an origin within a travel budget.</summary>
        Task<ServiceAreaResultDto> ComputeServiceAreaAsync(
            ServiceAreaRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
