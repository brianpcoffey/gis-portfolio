using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class SpatialGeometryNativeInterop
    {
        private const string LibName = "spatial_geometry_kernel";

        [DllImport(LibName, EntryPoint = "Geometry_TriangulateFan", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int TriangulateFan(
            [In] CoordinateNative[] points,
            int pointCount,
            [Out] TriangleNative[] triangles,
            int triangleCapacity,
            out int triangleCount);

        [DllImport(LibName, EntryPoint = "Geometry_ClipToBoundingBox", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int ClipToBoundingBox(
            [In] CoordinateNative[] subject,
            int subjectCount,
            in BoundingBoxNative bounds,
            [Out] CoordinateNative[] output,
            int outputCapacity,
            out int outputCount);
    }
}
