using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class VrpSolverNativeInterop
    {
        private const string LibName = "vrp_solver_kernel";

        [DllImport(LibName, EntryPoint = "Vrp_SolveCvrptw", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int SolveCvrptw(
            [In] double[] costMatrix,
            [In] double[] travelTimeMatrix,
            int matrixDim,
            [In] VrpStopNative[] stops,
            int stopCount,
            double vehicleCapacity,
            int maxVehicles,
            double shiftStartMinutes,
            double shiftEndMinutes,
            double vehicleFixedCost,
            int maxIterations,
            [Out] int[] outRouteStops,
            int routeStopsCapacity,
            [Out] int[] outRouteLengths,
            int routeLengthsCapacity,
            [Out] double[] outIterationCosts,
            int iterationCapacity,
            [Out] int[] outIterationCount);
    }
}
