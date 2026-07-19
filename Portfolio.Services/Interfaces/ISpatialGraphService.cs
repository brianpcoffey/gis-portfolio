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

        /// <summary>
        /// Computes the shortest-path cost from one origin to every node. The result is
        /// parallel to <paramref name="graph"/>.Nodes — index i is the cost to Nodes[i],
        /// not to node id i — and unreachable nodes receive positive infinity.
        /// </summary>
        Task<double[]> ComputeDistancesAsync(
            RoadGraphDto graph,
            int originNodeId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Computes a row-major shortest-path cost matrix between the given source and
        /// target nodes: index <c>s * targetIds.Count + t</c> is the cost from
        /// <c>sourceIds[s]</c> to <c>targetIds[t]</c>. Unreachable pairs receive infinity.
        /// </summary>
        Task<double[]> ComputeDistanceMatrixAsync(
            RoadGraphDto graph,
            IReadOnlyList<int> sourceIds,
            IReadOnlyList<int> targetIds,
            CancellationToken cancellationToken = default);

        /// <summary>Returns the id of the graph node nearest to the given coordinate (haversine).</summary>
        int SnapToNearestNode(RoadGraphDto graph, double latitude, double longitude);
    }
}
