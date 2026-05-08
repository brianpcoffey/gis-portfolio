using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class SpatialGraphNativeInterop
    {
        private const string LibName = "spatial_graph_engine";

        [DllImport(LibName, EntryPoint = "Graph_FindShortestPath", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int FindShortestPath(
            [In] GraphNodeNative[] nodes,
            int nodeCount,
            [In] GraphEdgeNative[] edges,
            int edgeCount,
            int startNodeId,
            int endNodeId,
            [Out] int[] outputNodeIds,
            int outputCapacity,
            out GraphPathResultNative result);

        [DllImport(LibName, EntryPoint = "Graph_ComputeServiceArea", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int ComputeServiceArea(
            [In] GraphNodeNative[] nodes,
            int nodeCount,
            [In] GraphEdgeNative[] edges,
            int edgeCount,
            int originNodeId,
            double maxCost,
            [Out] int[] reachableNodeIds,
            int outputCapacity,
            out int reachableCount);
    }
}
