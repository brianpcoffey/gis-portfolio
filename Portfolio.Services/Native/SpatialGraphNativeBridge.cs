using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class SpatialGraphNativeBridge
    {
        private static readonly bool _available;

        static SpatialGraphNativeBridge()
        {
            try
            {
                if (NativeToggle.Disabled)
                {
                    _available = false;
                    return;
                }

                _available = NativeLibrary.TryLoad(
                    "spatial_graph_engine",
                    typeof(SpatialGraphNativeBridge).Assembly,
                    DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory,
                    out _);
            }
            catch
            {
                _available = false;
            }
        }

        internal static bool IsAvailable => _available;

        internal static void LogAvailability(ILogger logger)
        {
            logger.LogInformation(_available
                ? "Native spatial graph engine loaded. Network operations will use the C++ fast path."
                : "Native spatial graph engine unavailable; using managed graph implementation.");
        }

        internal static bool TryFindShortestPath(
            RouteRequestDto request,
            ILogger logger,
            out RouteResultDto? result)
        {
            result = null;
            if (!_available)
                return false;

            try
            {
                result = FindShortestPath(request);
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native spatial graph routing failed; falling back to managed graph implementation.");
                return false;
            }
        }

        internal static bool TryComputeServiceArea(
            ServiceAreaRequestDto request,
            ILogger logger,
            out ServiceAreaResultDto? result)
        {
            result = null;
            if (!_available)
                return false;

            try
            {
                result = ComputeServiceArea(request);
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native spatial graph service-area computation failed; falling back to managed graph implementation.");
                return false;
            }
        }

        internal static bool TryComputeDistances(
            IReadOnlyList<GraphNodeDto> nodes,
            IReadOnlyList<GraphEdgeDto> edges,
            int originNodeId,
            ILogger logger,
            out double[]? distances)
        {
            distances = null;
            if (!_available)
                return false;

            try
            {
                var nativeNodes = nodes.Select(MapNode).ToArray();
                var nativeEdges = edges.Select(MapEdge).ToArray();
                var output = new double[Math.Max(1, nativeNodes.Length)];

                var status = SpatialGraphNativeInterop.ComputeDistances(
                    nativeNodes, nativeNodes.Length,
                    nativeEdges, nativeEdges.Length,
                    originNodeId,
                    output, output.Length);

                // A non-negative status is the reachable-node count, not a failure.
                if (status < 0)
                    throw new InvalidOperationException($"Native spatial graph engine failed with status {status}.");

                distances = output;
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native one-to-all distance computation failed; falling back to managed graph implementation.");
                return false;
            }
        }

        internal static bool TryComputeDistanceMatrix(
            IReadOnlyList<GraphNodeDto> nodes,
            IReadOnlyList<GraphEdgeDto> edges,
            IReadOnlyList<int> sourceIds,
            IReadOnlyList<int> targetIds,
            ILogger logger,
            out double[]? matrix)
        {
            matrix = null;
            if (!_available)
                return false;

            try
            {
                var nativeNodes = nodes.Select(MapNode).ToArray();
                var nativeEdges = edges.Select(MapEdge).ToArray();
                var sources = sourceIds.ToArray();
                var targets = targetIds.ToArray();
                var output = new double[Math.Max(1, sources.Length * targets.Length)];

                var status = SpatialGraphNativeInterop.ComputeDistanceMatrix(
                    nativeNodes, nativeNodes.Length,
                    nativeEdges, nativeEdges.Length,
                    sources, sources.Length,
                    targets, targets.Length,
                    output, output.Length);

                ThrowIfFailed(status);

                matrix = output;
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native distance-matrix computation failed; falling back to managed graph implementation.");
                return false;
            }
        }

        internal static RouteResultDto FindShortestPath(RouteRequestDto request)
        {
            var nodes = request.Nodes.Select(MapNode).ToArray();
            var edges = request.Edges.Select(MapEdge).ToArray();
            var output = new int[Math.Max(1, nodes.Length)];
            // The search settles at most every node once, so the node count is an exact
            // upper bound on the explored set — it can never be truncated.
            var explored = new int[Math.Max(1, nodes.Length)];
            var status = SpatialGraphNativeInterop.FindShortestPath(
                nodes, nodes.Length,
                edges, edges.Length,
                request.StartNodeId, request.EndNodeId,
                output, output.Length,
                out var result,
                explored, explored.Length,
                out var exploredCount);
            ThrowIfFailed(status);

            return new RouteResultDto
            {
                NativeAccelerated = true,
                Found = result.Found == 1,
                TotalCost = result.TotalCost,
                NodeIds = output.Take(result.PathCount).ToList(),
                ExploredNodes = exploredCount,
                ExploredNodeIds = explored.Take(exploredCount).ToList()
            };
        }

        internal static ServiceAreaResultDto ComputeServiceArea(ServiceAreaRequestDto request)
        {
            var nodes = request.Nodes.Select(MapNode).ToArray();
            var edges = request.Edges.Select(MapEdge).ToArray();
            var output = new int[Math.Max(1, nodes.Length)];
            var status = SpatialGraphNativeInterop.ComputeServiceArea(nodes, nodes.Length, edges, edges.Length, request.OriginNodeId, request.MaxCost, output, output.Length, out var reachableCount);
            ThrowIfFailed(status);

            return new ServiceAreaResultDto
            {
                NativeAccelerated = true,
                ReachableNodeIds = output.Take(reachableCount).ToList()
            };
        }

        private static GraphNodeNative MapNode(GraphNodeDto dto)
        {
            return new GraphNodeNative { Id = dto.Id, Latitude = dto.Latitude, Longitude = dto.Longitude };
        }

        private static GraphEdgeNative MapEdge(GraphEdgeDto dto)
        {
            return new GraphEdgeNative { FromNodeId = dto.FromNodeId, ToNodeId = dto.ToNodeId, Cost = dto.Cost, Bidirectional = dto.Bidirectional ? 1 : 0 };
        }

        private static void ThrowIfFailed(int status)
        {
            if (status != 0)
                throw new InvalidOperationException($"Native spatial graph engine failed with status {status}.");
        }

        private static bool IsNativeInvocationException(Exception exception)
        {
            return exception is DllNotFoundException
                or EntryPointNotFoundException
                or BadImageFormatException
                or SEHException
                or InvalidOperationException;
        }
    }
}
