using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class ViewshedNativeInterop
    {
        private const string LibName = "viewshed_kernel";

        [DllImport(LibName, EntryPoint = "Viewshed_Compute", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int Compute(
            [In] double[] elevation,
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
