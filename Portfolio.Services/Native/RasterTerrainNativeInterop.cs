using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class RasterTerrainNativeInterop
    {
        private const string LibName = "raster_terrain_kernel";

        [DllImport(LibName, EntryPoint = "Raster_GenerateHillshade", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int GenerateHillshade(
            [In] double[] elevation,
            int width,
            int height,
            double cellSize,
            double azimuthDegrees,
            double altitudeDegrees,
            [Out] byte[] intensities,
            int outputLength);

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
