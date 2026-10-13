using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class RasterTerrainNativeInterop
    {
        private const string LibName = "raster_terrain_kernel";

        // Mirrors Raster_GenerateHillshade in native/raster_terrain_kernel/include/raster_terrain_kernel.h, which
        // documents the parameters, ownership and status codes. elevationLength is the element count of elevation
        // and the kernel requires it to equal width * height.
        [DllImport(LibName, EntryPoint = "Raster_GenerateHillshade", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int GenerateHillshade(
            [In] double[] elevation,
            int elevationLength,
            int width,
            int height,
            double cellSize,
            double azimuthDegrees,
            double altitudeDegrees,
            [Out] byte[] intensities,
            int outputLength);

        // Mirrors Raster_GenerateHeatmap in the same header. pointCount is the element count of points and
        // outputLength the element count of values.
        [DllImport(LibName, EntryPoint = "Raster_GenerateHeatmap", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int GenerateHeatmap(
            [In] WeightedPointNative[] points,
            int pointCount,
            in RasterExtentNative extent,
            int width,
            int height,
            double radius,
            [Out] double[] values,
            int outputLength);
    }
}
