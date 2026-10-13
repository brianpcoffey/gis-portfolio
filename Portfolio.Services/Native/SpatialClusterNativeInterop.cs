using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class SpatialClusterNativeInterop
    {
        private const string LibName = "spatial_cluster_kernel";

        /// <summary>
        /// Runs DBSCAN over <paramref name="count"/> points; the caller owns both arrays, and
        /// <paramref name="labels"/> must have at least <paramref name="outputLength"/> elements, which must be
        /// at least <paramref name="count"/>. Returns the cluster count (0 or more), or -1 (invalid argument) or
        /// -99 (native exception); the full contract is in spatial_cluster_kernel.h.
        /// </summary>
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
