using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class FacilityLocationNativeInterop
    {
        private const string LibName = "facility_location_kernel";

        [DllImport(LibName, EntryPoint = "Facility_SolvePMedian", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int SolvePMedian(
            [In] double[] costMatrix,
            int candidateCount,
            int demandCount,
            [In] double[] demandWeights,
            int facilityCount,
            int objectiveMode,
            double coverageThreshold,
            int maxIterations,
            [Out] int[] chosenCandidates, int chosenCapacity,
            [Out] double[] iterationObjectives, int iterationCapacity,
            [Out] int[] iterationCount);

        [DllImport(LibName, EntryPoint = "Facility_EvaluateCoverage", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int EvaluateCoverage(
            [In] double[] costMatrix,
            int candidateCount,
            int demandCount,
            [In] double[] demandWeights,
            [In] int[] chosenCandidates,
            int chosenCount,
            [Out] int[] assignment,
            [Out] double[] responseTimes,
            int outputCapacity,
            [Out] double[] mean,
            [Out] double[] p50,
            [Out] double[] p90,
            [Out] double[] pctWithinFirstThreshold,
            [Out] double[] pctWithinSecondThreshold,
            double firstThreshold,
            double secondThreshold);
    }
}
