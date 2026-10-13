using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Data;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Native;

namespace Portfolio.Services.Services
{
    public class SpatialGraphService : ISpatialGraphService
    {
        private const int MaxNodeCount = 5000;
        private const int MaxEdgeCount = 20000;
        private const int MaxMatrixCells = 250_000;
        // Average road speed assumed for travel-time estimates (km/h).
        private const double AvgSpeedKmh = 40.0;

        // Built once at first access; the static graph never changes at runtime.
        // Shared instance: callers must never mutate it.
        private static readonly RoadGraphDto _cachedGraph = RedlandsRoadNetwork.Build();

        private readonly ILogger<SpatialGraphService> _logger;

        public SpatialGraphService(ILogger<SpatialGraphService> logger)
        {
            _logger = logger;
            SpatialGraphNativeBridge.LogAvailability(_logger);
        }

        // Returns the pre-built Redlands road network graph (served from cache).
        public Task<RoadGraphDto> GetRedlandsGraphAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_cachedGraph);
        }

        // Finds the least-cost path through a spatial graph using native code when available.
        public Task<RouteResultDto> FindShortestPathAsync(
            RouteRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateGraph(request.Nodes, request.Edges);

            // Single O(n) pass to verify both terminal nodes exist.
            var nodeIds = NodeIdSet(request.Nodes);
            if (!nodeIds.Contains(request.StartNodeId) || !nodeIds.Contains(request.EndNodeId))
                throw new ArgumentException("Start and end nodes must exist in the graph.", nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            // Only Dijkstra is forwarded to the native bridge; A* is managed-only.
            var algo = (request.Algorithm ?? "dijkstra").Trim().ToLowerInvariant();
            if (algo != "astar" && SpatialGraphNativeBridge.TryFindShortestPath(request, _logger, out var nativeResult))
            {
                nativeResult!.TotalCost = Math.Round(nativeResult.TotalCost, 2);
                nativeResult.Path = MapPath(nativeResult.NodeIds, BuildNodeLookup(request.Nodes));
                nativeResult.AlgorithmUsed = "dijkstra";
                EnrichMetrics(nativeResult, request.Nodes);
                return Task.FromResult(nativeResult);
            }

            var result = algo == "astar"
                ? FindShortestPathAStar(request, cancellationToken)
                : FindShortestPathDijkstra(request, cancellationToken);

            return Task.FromResult(result);
        }

        // Computes all nodes reachable from an origin within the supplied cost budget.
        public Task<ServiceAreaResultDto> ComputeServiceAreaAsync(
            ServiceAreaRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateGraph(request.Nodes, request.Edges);

            var nodeIds = NodeIdSet(request.Nodes);
            if (!nodeIds.Contains(request.OriginNodeId))
                throw new ArgumentException("Origin node must exist in the graph.", nameof(request));
            if (request.MaxCost < 0 || !double.IsFinite(request.MaxCost))
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

        // Computes the cost from one origin to every node, parallel to graph.Nodes.
        // Unlike ComputeServiceAreaAsync this keeps the costs, which is what isochrone
        // banding and facility-location assignment need.
        public Task<double[]> ComputeDistancesAsync(
            RoadGraphDto graph,
            int originNodeId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(graph);
            ValidateGraph(graph.Nodes, graph.Edges);

            var nodeIds = NodeIdSet(graph.Nodes);
            if (!nodeIds.Contains(originNodeId))
                throw new ArgumentException("Origin node must exist in the graph.", nameof(originNodeId));

            cancellationToken.ThrowIfCancellationRequested();

            if (SpatialGraphNativeBridge.TryComputeDistances(graph.Nodes, graph.Edges, originNodeId, _logger, out var native))
                return Task.FromResult(native!);

            var distances = ComputeDistances(graph.Nodes, graph.Edges, originNodeId, cancellationToken);
            var result = new double[graph.Nodes.Count];
            for (var i = 0; i < graph.Nodes.Count; i++)
                result[i] = distances.TryGetValue(graph.Nodes[i].Id, out var d) ? d : double.PositiveInfinity;

            return Task.FromResult(result);
        }

        // Row-major cost matrix between two node sets. The managed fallback builds the
        // adjacency structure once and reuses it across every source, mirroring the native
        // kernel — rebuilding it per source is what makes a naive matrix quadratic.
        public Task<double[]> ComputeDistanceMatrixAsync(
            RoadGraphDto graph,
            IReadOnlyList<int> sourceIds,
            IReadOnlyList<int> targetIds,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(graph);
            ValidateGraph(graph.Nodes, graph.Edges);

            if (sourceIds is null || sourceIds.Count == 0)
                throw new ArgumentException("At least one source node is required.", nameof(sourceIds));
            if (targetIds is null || targetIds.Count == 0)
                throw new ArgumentException("At least one target node is required.", nameof(targetIds));
            if ((long)sourceIds.Count * targetIds.Count > MaxMatrixCells)
                throw new ArgumentException($"Distance matrices are limited to {MaxMatrixCells} cells.", nameof(sourceIds));

            var nodeIds = NodeIdSet(graph.Nodes);
            RequireAllNodes(sourceIds, nodeIds, "Every source node must exist in the graph.", nameof(sourceIds));
            RequireAllNodes(targetIds, nodeIds, "Every target node must exist in the graph.", nameof(targetIds));

            cancellationToken.ThrowIfCancellationRequested();

            if (SpatialGraphNativeBridge.TryComputeDistanceMatrix(graph.Nodes, graph.Edges, sourceIds, targetIds, _logger, out var native))
                return Task.FromResult(native!);

            var adjacency = BuildAdjacency(graph.Edges, graph.Nodes.Count);
            var matrix = new double[sourceIds.Count * targetIds.Count];

            for (var s = 0; s < sourceIds.Count; s++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var distances = ComputeDistances(adjacency, graph.Nodes, sourceIds[s], cancellationToken);
                for (var t = 0; t < targetIds.Count; t++)
                {
                    matrix[s * targetIds.Count + t] =
                        distances.TryGetValue(targetIds[t], out var d) ? d : double.PositiveInfinity;
                }
            }

            return Task.FromResult(matrix);
        }

        // Linear scan over the node list. At ~2,500 nodes this is well under a millisecond,
        // so a spatial index would be complexity without benefit.
        public int SnapToNearestNode(RoadGraphDto graph, double latitude, double longitude)
        {
            ArgumentNullException.ThrowIfNull(graph);
            if (graph.Nodes is null || graph.Nodes.Count == 0)
                throw new ArgumentException("Graph must contain at least one node.", nameof(graph));
            if (!double.IsFinite(latitude) || !double.IsFinite(longitude))
                throw new ArgumentException("Coordinates must be finite values.", nameof(latitude));

            var probe = new GraphNodeDto { Latitude = latitude, Longitude = longitude };
            var bestId = graph.Nodes[0].Id;
            var bestDistance = double.PositiveInfinity;

            foreach (var node in graph.Nodes)
            {
                var d = Haversine(probe, node);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    bestId = node.Id;
                }
            }

            return bestId;
        }

        private static RouteResultDto FindShortestPathDijkstra(RouteRequestDto request, CancellationToken cancellationToken)
            => SearchShortestPath(request, null, "dijkstra", cancellationToken);

        private static RouteResultDto FindShortestPathAStar(RouteRequestDto request, CancellationToken cancellationToken)
        {
            var nodeLookup = BuildNodeLookup(request.Nodes);
            if (!nodeLookup.TryGetValue(request.EndNodeId, out var endNode))
                throw new ArgumentException("End node not found.", nameof(request));

            double Heuristic(int id) => nodeLookup.TryGetValue(id, out var node)
                ? Haversine(node, endNode)
                : 0;

            return SearchShortestPath(request, Heuristic, "astar", cancellationToken);
        }

        // One best-first search shared by Dijkstra (heuristic == null, priority = cost so far)
        // and A* (priority = cost so far + heuristic). Edges to undeclared nodes are dropped,
        // as the native kernel does when it builds its CSR adjacency.
        private static RouteResultDto SearchShortestPath(
            RouteRequestDto request,
            Func<int, double>? heuristic,
            string algorithmName,
            CancellationToken cancellationToken)
        {
            var cost = new Dictionary<int, double>(request.Nodes.Count);
            foreach (var n in request.Nodes) cost[n.Id] = double.PositiveInfinity;

            var previous = new Dictionary<int, int>(request.Nodes.Count);
            var closed = new HashSet<int>(request.Nodes.Count);
            var exploredOrder = new List<int>();
            var open = new PriorityQueue<int, double>(request.Nodes.Count);

            cost[request.StartNodeId] = 0;
            open.Enqueue(request.StartNodeId, heuristic?.Invoke(request.StartNodeId) ?? 0);

            var adjacency = BuildAdjacency(request.Edges, request.Nodes.Count);
            while (open.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = open.Dequeue();
                if (!closed.Add(current))
                    continue;
                exploredOrder.Add(current);
                if (current == request.EndNodeId)
                    break;
                if (adjacency.TryGetValue(current, out var edges))
                    RelaxEdges(current, edges, cost, previous, closed, open, heuristic);
            }

            return BuildRouteResult(request, cost, previous, exploredOrder, algorithmName);
        }

        private static void RelaxEdges(
            int current,
            List<AdjEdge> edges,
            Dictionary<int, double> cost,
            Dictionary<int, int> previous,
            HashSet<int> closed,
            PriorityQueue<int, double> open,
            Func<int, double>? heuristic)
        {
            var curCost = cost[current];
            foreach (var edge in edges)
            {
                if (closed.Contains(edge.To))
                    continue;
                if (!cost.TryGetValue(edge.To, out var known))
                    continue;
                var candidate = curCost + edge.Cost;
                if (candidate >= known)
                    continue;

                cost[edge.To] = candidate;
                previous[edge.To] = current;
                open.Enqueue(edge.To, candidate + (heuristic?.Invoke(edge.To) ?? 0));
            }
        }

        private static RouteResultDto BuildRouteResult(
            RouteRequestDto request,
            Dictionary<int, double> cost,
            Dictionary<int, int> previous,
            List<int> exploredOrder,
            string algorithmName)
        {
            if (double.IsPositiveInfinity(cost[request.EndNodeId]))
            {
                return new RouteResultDto
                {
                    NativeAccelerated = false,
                    Found = false,
                    TotalCost = 0,
                    AlgorithmUsed = algorithmName,
                    ExploredNodes = exploredOrder.Count,
                    ExploredNodeIds = exploredOrder
                };
            }

            var path = ReconstructPath(previous, request.StartNodeId, request.EndNodeId);
            var dto = new RouteResultDto
            {
                NativeAccelerated = false,
                Found = true,
                TotalCost = Math.Round(cost[request.EndNodeId], 2),
                NodeIds = path,
                Path = MapPath(path, BuildNodeLookup(request.Nodes)),
                AlgorithmUsed = algorithmName,
                ExploredNodes = exploredOrder.Count,
                ExploredNodeIds = exploredOrder
            };
            EnrichMetrics(dto, request.Nodes);
            return dto;
        }

        // Computes haversine great-circle distance in km between two graph nodes.
        private static double Haversine(GraphNodeDto a, GraphNodeDto b)
            => HaversineKm(a.Latitude, a.Longitude, b.Latitude, b.Longitude);

        // Operation order is load-bearing: results must stay bit-identical.
        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);
            var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
        }

        private static double ToRad(double deg) => deg * Math.PI / 180.0;

        // Fills DistanceKm and EstimatedMinutes from the node coordinate path.
        private static void EnrichMetrics(RouteResultDto dto, IReadOnlyList<GraphNodeDto> nodes)
        {
            if (dto.Path.Count < 2)
            {
                dto.DistanceKm = 0;
                dto.EstimatedMinutes = 0;
                return;
            }

            double total = 0;
            for (var i = 1; i < dto.Path.Count; i++)
            {
                var a = dto.Path[i - 1];
                var b = dto.Path[i];
                // X = longitude, Y = latitude in CoordinateDto.
                total += HaversineKm(a.Y, a.X, b.Y, b.X);
            }

            dto.DistanceKm = Math.Round(total, 2);
            dto.EstimatedMinutes = Math.Round(total / AvgSpeedKmh * 60, 1);
        }

        private static Dictionary<int, double> ComputeDistances(IReadOnlyList<GraphNodeDto> nodes, IReadOnlyList<GraphEdgeDto> edges, int originNodeId, CancellationToken cancellationToken)
        {
            var adjacency = BuildAdjacency(edges, nodes.Count);
            return ComputeDistances(adjacency, nodes, originNodeId, cancellationToken);
        }

        // Single-source Dijkstra over a prebuilt adjacency structure. Split out so the
        // distance matrix can build the adjacency once and reuse it across every source
        // rather than rebuilding it per search.
        private static Dictionary<int, double> ComputeDistances(
            Dictionary<int, List<AdjEdge>> adjacency,
            IReadOnlyList<GraphNodeDto> nodes,
            int originNodeId,
            CancellationToken cancellationToken)
        {
            var distances = new Dictionary<int, double>(nodes.Count);
            foreach (var n in nodes) distances[n.Id] = double.PositiveInfinity;

            var settled = new HashSet<int>(nodes.Count);
            var queue = new PriorityQueue<int, double>(nodes.Count);

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

                var curDist = distances[current];
                foreach (var edge in outgoing)
                {
                    if (settled.Contains(edge.To))
                        continue;
                    // Edges may reference a node that was never declared. Skip them rather
                    // than indexing (which throws) — this mirrors the native kernel, which
                    // drops undeclared edges when it builds its CSR adjacency.
                    if (!distances.TryGetValue(edge.To, out var known))
                        continue;

                    var candidate = curDist + edge.Cost;
                    if (candidate >= known)
                        continue;

                    distances[edge.To] = candidate;
                    queue.Enqueue(edge.To, candidate);
                }
            }

            return distances;
        }

        // Lightweight value type used in the adjacency list — avoids allocating
        // a full GraphEdgeDto heap object per directed edge in the hot routing loop.
        private readonly record struct AdjEdge(int To, double Cost);

        private static Dictionary<int, List<AdjEdge>> BuildAdjacency(IEnumerable<GraphEdgeDto> edges, int nodeCapacity)
        {
            var adjacency = new Dictionary<int, List<AdjEdge>>(nodeCapacity);
            foreach (var edge in edges)
            {
                Add(edge.FromNodeId, edge.ToNodeId, edge.Cost);
                if (edge.Bidirectional)
                    Add(edge.ToNodeId, edge.FromNodeId, edge.Cost);
            }

            return adjacency;

            void Add(int from, int to, double cost)
            {
                if (!adjacency.TryGetValue(from, out var list))
                {
                    list = new List<AdjEdge>(4);
                    adjacency[from] = list;
                }

                list.Add(new AdjEdge(to, cost));
            }
        }

        private static void RequireAllNodes(IReadOnlyList<int> ids, HashSet<int> nodeIds, string message, string paramName)
        {
            foreach (var id in ids)
                if (!nodeIds.Contains(id))
                    throw new ArgumentException(message, paramName);
        }

        private static HashSet<int> NodeIdSet(IReadOnlyList<GraphNodeDto> nodes)
        {
            var ids = new HashSet<int>(nodes.Count);
            foreach (var n in nodes) ids.Add(n.Id);
            return ids;
        }

        // Builds a node-id → node lookup dictionary from the request node list.
        private static Dictionary<int, GraphNodeDto> BuildNodeLookup(IReadOnlyList<GraphNodeDto> nodes)
        {
            var lookup = new Dictionary<int, GraphNodeDto>(nodes.Count);
            foreach (var n in nodes) lookup[n.Id] = n;
            return lookup;
        }

        // Uses a Stack to avoid List+Reverse — O(n) with no second pass.
        private static List<int> ReconstructPath(Dictionary<int, int> previous, int startNodeId, int endNodeId)
        {
            var stack = new Stack<int>();
            var current = endNodeId;
            while (current != startNodeId)
            {
                stack.Push(current);
                current = previous[current];
            }

            stack.Push(startNodeId);
            return [.. stack];
        }

        // Single TryGetValue per node — avoids double lookup from ContainsKey + indexer.
        private static List<CoordinateDto> MapPath(IEnumerable<int> nodeIds, Dictionary<int, GraphNodeDto> nodeLookup)
        {
            var coords = new List<CoordinateDto>();
            foreach (var id in nodeIds)
            {
                if (nodeLookup.TryGetValue(id, out var node))
                    coords.Add(new CoordinateDto { X = node.Longitude, Y = node.Latitude });
            }

            return coords;
        }

        private static void ValidateGraph(IReadOnlyList<GraphNodeDto>? nodes, IReadOnlyList<GraphEdgeDto>? edges)
        {
            ValidateCounts(nodes, edges);
            ValidateNodeCoordinates(nodes!);
            ValidateEdgeCosts(edges!);
        }

        private static void ValidateCounts(IReadOnlyList<GraphNodeDto>? nodes, IReadOnlyList<GraphEdgeDto>? edges)
        {
            if (nodes is null || nodes.Count == 0)
                throw new ArgumentException("At least one graph node is required.");
            if (nodes.Count > MaxNodeCount)
                throw new ArgumentException($"Graph operations are limited to {MaxNodeCount} nodes.");
            if (edges is null || edges.Count == 0)
                throw new ArgumentException("At least one graph edge is required.");
            if (edges.Count > MaxEdgeCount)
                throw new ArgumentException($"Graph operations are limited to {MaxEdgeCount} edges.");
        }

        private static void ValidateNodeCoordinates(IReadOnlyList<GraphNodeDto> nodes)
        {
            foreach (var n in nodes)
            {
                if (!double.IsFinite(n.Latitude)
                    || !double.IsFinite(n.Longitude))
                    throw new ArgumentException("Graph node coordinates must be finite numeric values.");
            }
        }

        private static void ValidateEdgeCosts(IReadOnlyList<GraphEdgeDto> edges)
        {
            foreach (var e in edges)
            {
                if (e.Cost < 0 || !double.IsFinite(e.Cost))
                    throw new ArgumentException("Graph edge costs must be finite non-negative values.");
            }
        }
    }
}
