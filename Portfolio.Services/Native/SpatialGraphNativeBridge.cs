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

        internal static RouteResultDto FindShortestPath(RouteRequestDto request)
        {
            var nodes = request.Nodes.Select(MapNode).ToArray();
            var edges = request.Edges.Select(MapEdge).ToArray();
            var output = new int[Math.Max(1, nodes.Length)];
            var status = SpatialGraphNativeInterop.FindShortestPath(nodes, nodes.Length, edges, edges.Length, request.StartNodeId, request.EndNodeId, output, output.Length, out var result);
            ThrowIfFailed(status);

            return new RouteResultDto
            {
                NativeAccelerated = true,
                Found = result.Found == 1,
                TotalCost = result.TotalCost,
                NodeIds = output.Take(result.PathCount).ToList()
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
