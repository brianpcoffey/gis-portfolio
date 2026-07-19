using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class SpatialClusterNativeInterop
    {
        private const string LibName = "spatial_cluster_kernel";

        [DllImport(LibName, EntryPoint = "Cluster_RunDbscan", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int RunDbscan(
            [In] ClusterPointNative[] points,
            int count,
            double epsilon,
            int minPoints,
            [Out] int[] labels,
            int outputLength);
    }
}
