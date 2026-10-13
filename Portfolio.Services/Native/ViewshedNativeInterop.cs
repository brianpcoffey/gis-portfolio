using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class ViewshedNativeInterop
    {
        private const string LibName = "viewshed_kernel";

        // Mirrors Viewshed_Compute in native/viewshed_kernel/include/viewshed_kernel.h, which documents the
        // parameters, ownership and status codes. elevationLength is the element count of elevation and the
        // kernel requires it to equal width * height.
        [DllImport(LibName, EntryPoint = "Viewshed_Compute", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int Compute(
            [In] double[] elevation,
            int elevationLength,
            int width,
            int height,
            double cellSize,
            int observerX,
            int observerY,
            double observerHeight,
            [Out] byte[] visibility,
            int outputLength);
    }
}
