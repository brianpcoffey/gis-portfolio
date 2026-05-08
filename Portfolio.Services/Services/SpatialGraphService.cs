using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Native;

namespace Portfolio.Services.Services
{
    public class SpatialGraphService : ISpatialGraphService
    {
        private const int MaxNodeCount = 5000;
        private const int MaxEdgeCount = 20000;
        private readonly ILogger<SpatialGraphService> _logger;

        public SpatialGraphService(ILogger<SpatialGraphService> logger)
        {
            _logger = logger;
            SpatialGraphNativeBridge.LogAvailability(_logger);
        }

        // Finds the least-cost path through a spatial graph using native code when available.
        public Task<RouteResultDto> FindShortestPathAsync(
            RouteRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateGraph(request.Nodes, request.Edges);
            if (request.Nodes.All(n => n.Id != request.StartNodeId) || request.Nodes.All(n => n.Id != request.EndNodeId))
                throw new ArgumentException("Start and end nodes must exist in the graph.", nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            if (SpatialGraphNativeBridge.TryFindShortestPath(request, _logger, out var nativeResult))
            {
                nativeResult!.TotalCost = Math.Round(nativeResult.TotalCost, 2);
                nativeResult.Path = MapPath(nativeResult.NodeIds, request.Nodes);
                return Task.FromResult(nativeResult);
            }

            return Task.FromResult(FindShortestPathManaged(request, cancellationToken));
        }

        // Computes all nodes reachable from an origin within the supplied cost budget.
        public Task<ServiceAreaResultDto> ComputeServiceAreaAsync(
            ServiceAreaRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateGraph(request.Nodes, request.Edges);
            if (request.Nodes.All(n => n.Id != request.OriginNodeId))
                throw new ArgumentException("Origin node must exist in the graph.", nameof(request));
            if (request.MaxCost < 0 || double.IsNaN(request.MaxCost) || double.IsInfinity(request.MaxCost))
                throw new ArgumentException("Maximum cost must be a finite non-negative value.", nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            if (SpatialGraphNativeBridge.TryComputeServiceArea(request, _logger, out var nativeResult))
            {
                nativeResult!.ReachableNodeIds = nativeResult.ReachableNodeIds.OrderBy(id => id).ToList();
                return Task.FromResult(nativeResult);
            }

            var distances = ComputeDistances(request.Nodes, request.Edges, request.OriginNodeId, cancellationToken);
            return Task.FromResult(new ServiceAreaResultDto
            {
                NativeAccelerated = false,
                ReachableNodeIds = distances
                    .Where(kvp => kvp.Value <= request.MaxCost)
                    .Select(kvp => kvp.Key)
                    .OrderBy(id => id)
                    .ToList()
            });
        }

        private static RouteResultDto FindShortestPathManaged(RouteRequestDto request, CancellationToken cancellationToken)
        {
            var distances = request.Nodes.ToDictionary(n => n.Id, _ => double.PositiveInfinity);
            var previous = new Dictionary<int, int>();
            var settled = new HashSet<int>();
            var queue = new PriorityQueue<int, double>();

            distances[request.StartNodeId] = 0;
            queue.Enqueue(request.StartNodeId, 0);

            var adjacency = BuildAdjacency(request.Edges);
            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = queue.Dequeue();
                if (!settled.Add(current))
                    continue;
                if (current == request.EndNodeId)
                    break;
                if (!adjacency.TryGetValue(current, out var edges))
                    continue;

                foreach (var edge in edges)
                {
                    if (settled.Contains(edge.ToNodeId))
                        continue;
                    var candidate = distances[current] + edge.Cost;
                    if (candidate >= distances[edge.ToNodeId])
                        continue;

                    distances[edge.ToNodeId] = candidate;
                    previous[edge.ToNodeId] = current;
                    queue.Enqueue(edge.ToNodeId, candidate);
                }
            }

            if (double.IsPositiveInfinity(distances[request.EndNodeId]))
            {
                return new RouteResultDto
                {
                    NativeAccelerated = false,
                    Found = false,
                    TotalCost = 0
                };
            }

            var path = ReconstructPath(previous, request.StartNodeId, request.EndNodeId);
            return new RouteResultDto
            {
                NativeAccelerated = false,
                Found = true,
                TotalCost = Math.Round(distances[request.EndNodeId], 2),
                NodeIds = path,
                Path = MapPath(path, request.Nodes)
            };
        }

        private static Dictionary<int, double> ComputeDistances(IReadOnlyList<GraphNodeDto> nodes, IReadOnlyList<GraphEdgeDto> edges, int originNodeId, CancellationToken cancellationToken)
        {
            var distances = nodes.ToDictionary(n => n.Id, _ => double.PositiveInfinity);
            var settled = new HashSet<int>();
            var queue = new PriorityQueue<int, double>();
            var adjacency = BuildAdjacency(edges);

            distances[originNodeId] = 0;
            queue.Enqueue(originNodeId, 0);

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = queue.Dequeue();
                if (!settled.Add(current))
                    continue;
                if (!adjacency.TryGetValue(current, out var outgoing))
                    continue;

                foreach (var edge in outgoing)
                {
                    if (settled.Contains(edge.ToNodeId))
                        continue;
                    var candidate = distances[current] + edge.Cost;
                    if (candidate >= distances[edge.ToNodeId])
                        continue;

                    distances[edge.ToNodeId] = candidate;
                    queue.Enqueue(edge.ToNodeId, candidate);
                }
            }

            return distances;
        }

        private static Dictionary<int, List<GraphEdgeDto>> BuildAdjacency(IEnumerable<GraphEdgeDto> edges)
        {
            var adjacency = new Dictionary<int, List<GraphEdgeDto>>();
            foreach (var edge in edges)
            {
                AddEdge(edge.FromNodeId, edge.ToNodeId, edge.Cost);
                if (edge.Bidirectional)
                    AddEdge(edge.ToNodeId, edge.FromNodeId, edge.Cost);
            }

            return adjacency;

            void AddEdge(int from, int to, double cost)
            {
                if (!adjacency.TryGetValue(from, out var list))
                {
                    list = [];
                    adjacency[from] = list;
                }

                list.Add(new GraphEdgeDto { FromNodeId = from, ToNodeId = to, Cost = cost, Bidirectional = false });
            }
        }

        private static List<int> ReconstructPath(Dictionary<int, int> previous, int startNodeId, int endNodeId)
        {
            var path = new List<int>();
            var current = endNodeId;
            while (current != startNodeId)
            {
                path.Add(current);
                current = previous[current];
            }

            path.Add(startNodeId);
            path.Reverse();
            return path;
        }

        private static List<CoordinateDto> MapPath(IEnumerable<int> nodeIds, IReadOnlyList<GraphNodeDto> nodes)
        {
            var nodeLookup = nodes.ToDictionary(n => n.Id);
            return nodeIds
                .Where(nodeLookup.ContainsKey)
                .Select(id => new CoordinateDto { X = nodeLookup[id].Longitude, Y = nodeLookup[id].Latitude })
                .ToList();
        }

        private static void ValidateGraph(IReadOnlyList<GraphNodeDto>? nodes, IReadOnlyList<GraphEdgeDto>? edges)
        {
            if (nodes is null || nodes.Count == 0)
                throw new ArgumentException("At least one graph node is required.");
            if (nodes.Count > MaxNodeCount)
                throw new ArgumentException($"Graph operations are limited to {MaxNodeCount} nodes.");
            if (edges is null || edges.Count == 0)
                throw new ArgumentException("At least one graph edge is required.");
            if (edges.Count > MaxEdgeCount)
                throw new ArgumentException($"Graph operations are limited to {MaxEdgeCount} edges.");
            if (nodes.Any(n => double.IsNaN(n.Latitude) || double.IsInfinity(n.Latitude) || double.IsNaN(n.Longitude) || double.IsInfinity(n.Longitude)))
                throw new ArgumentException("Graph node coordinates must be finite numeric values.");
            if (edges.Any(e => e.Cost < 0 || double.IsNaN(e.Cost) || double.IsInfinity(e.Cost)))
                throw new ArgumentException("Graph edge costs must be finite non-negative values.");
        }
    }
}
