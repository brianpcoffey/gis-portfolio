using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class CatRiskNativeInterop
    {
        private const string LibName = "cat_risk_kernel";

        [DllImport(LibName, EntryPoint = "Cat_ComputeRingAccumulation", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int ComputeRingAccumulation(
            [In] CatLocationNative[] locations,
            int locationCount,
            double radiusKm,
            [Out] double[] ringTiv,
            [Out] int[] neighborCount,
            int outputLength);

        [DllImport(LibName, EntryPoint = "Cat_SimulateEventLosses", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int SimulateEventLosses(
            [In] CatLocationNative[] locations,
            int locationCount,
            [In] CatEventNative[] events,
            int eventCount,
            double vulnerabilityAlpha,
            [Out] double[] eventLosses,
            [Out] int[] affectedCounts,
            int outputLength);
    }
}
