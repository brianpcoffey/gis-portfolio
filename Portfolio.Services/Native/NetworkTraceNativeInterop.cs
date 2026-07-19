using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class NetworkTraceNativeInterop
    {
        private const string LibName = "network_trace_kernel";

        [DllImport(LibName, EntryPoint = "Trace_Downstream", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int Downstream(
            [In] TraceElementNative[] elements,
            int elementCount,
            int sourceNodeId,
            int faultElementId,
            [Out] int[] elementIds,
            int outputCapacity,
            out int customersAffected);

        [DllImport(LibName, EntryPoint = "Trace_Upstream", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int Upstream(
            [In] TraceElementNative[] elements,
            int elementCount,
            int sourceNodeId,
            int faultElementId,
            [Out] int[] elementIds,
            int outputCapacity);

        [DllImport(LibName, EntryPoint = "Trace_FindIsolationDevices", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int FindIsolationDevices(
            [In] TraceElementNative[] elements,
            int elementCount,
            int sourceNodeId,
            int faultElementId,
            [Out] int[] deviceIds,
            int outputCapacity);

        [DllImport(LibName, EntryPoint = "Trace_ComputeEnergizedSet", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int ComputeEnergizedSet(
            [In] TraceElementNative[] elements,
            int elementCount,
            int sourceNodeId,
            [In] int[] overrideElementIds,
            [In] int[] overrideStates,
            int overrideCount,
            [Out] int[] energizedElementIds,
            int outputCapacity,
            out int customersServed);
    }
}
