using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class ViewshedNativeInterop
    {
        private const string LibName = "viewshed_kernel";

        // Must equal VIEWSHED_ABI_VERSION in native/viewshed_kernel/include/viewshed_kernel.h. ViewshedNativeBridge
        // refuses a library that reports another version (or none), so bump both together with any signature change.
        internal const int ExpectedAbiVersion = 2;

        [DllImport(LibName, EntryPoint = "Viewshed_AbiVersion", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int AbiVersion();

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
