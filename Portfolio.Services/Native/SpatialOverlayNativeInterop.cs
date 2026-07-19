using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class SpatialOverlayNativeInterop
    {
        private const string LibName = "spatial_overlay_kernel";

        [DllImport(LibName, EntryPoint = "Overlay_AssignPointsToZones", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int AssignPointsToZones(
            [In] OverlayPointNative[] points,
            int pointCount,
            [In] OverlayPointNative[] polygonVertices,
            int totalVertices,
            [In] int[] ringSizes,
            int zoneCount,
            [Out] int[] assignments,
            int outputLength);
    }
}
