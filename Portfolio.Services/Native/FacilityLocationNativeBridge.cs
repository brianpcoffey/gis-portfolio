using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class FacilityLocationNativeBridge
    {
        private static readonly bool _available;

        static FacilityLocationNativeBridge()
        {
            try
            {
                if (NativeToggle.Disabled)
                {
                    _available = false;
                    return;
                }

                _available = NativeLibrary.TryLoad(
                    "facility_location_kernel",
                    typeof(FacilityLocationNativeBridge).Assembly,
                    DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory,
                    out _);
            }
            catch
            {
                _available = false;
            }
        }

        internal static bool IsAvailable => _available;

        internal static void LogAvailability(ILogger logger)
        {
            logger.LogInformation(_available
                ? "Native facility location kernel loaded. Station siting and coverage evaluation will use the C++ fast path."
                : "Native facility location kernel unavailable; using managed facility location implementation.");
        }

        internal static bool TrySolvePMedian(
            double[] costMatrix,
            int candidateCount,
            int demandCount,
            double[] demandWeights,
            int facilityCount,
            int objectiveMode,
            double coverageThreshold,
            int maxIterations,
            ILogger logger,
            out FacilitySolveNativeResult? result)
        {
            result = null;
            if (!_available)
                return false;

            try
            {
                var chosen = new int[facilityCount];
                // One entry for the greedy seed plus one per applied substitution.
                var objectives = new double[maxIterations + 1];
                var iterationCount = new int[1];

                var status = FacilityLocationNativeInterop.SolvePMedian(
                    costMatrix,
                    candidateCount,
                    demandCount,
                    demandWeights,
                    facilityCount,
                    objectiveMode,
                    coverageThreshold,
                    maxIterations,
                    chosen, chosen.Length,
                    objectives, objectives.Length,
                    iterationCount);

                if (status < 0)
                    throw new InvalidOperationException($"Native facility location kernel failed with status {status}.");

                var written = Math.Clamp(iterationCount[0], 0, objectives.Length);
                var chosenCount = Math.Clamp(status, 0, chosen.Length);

                result = new FacilitySolveNativeResult
                {
                    ChosenCandidateIndices = chosen[..chosenCount],
                    IterationObjectives = objectives[..written]
                };
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native p-median solve failed; falling back to managed implementation.");
                return false;
            }
        }

        internal static bool TryEvaluateCoverage(
            double[] costMatrix,
            int candidateCount,
            int demandCount,
            double[] demandWeights,
            int[] chosenCandidateIndices,
            double firstThreshold,
            double secondThreshold,
            ILogger logger,
            out FacilityCoverageNativeResult? result)
        {
            result = null;
            if (!_available)
                return false;

            try
            {
                var assignment = new int[demandCount];
                var responseTimes = new double[demandCount];
                var mean = new double[1];
                var p50 = new double[1];
                var p90 = new double[1];
                var withinFirst = new double[1];
                var withinSecond = new double[1];

                var status = FacilityLocationNativeInterop.EvaluateCoverage(
                    costMatrix,
                    candidateCount,
                    demandCount,
                    demandWeights,
                    chosenCandidateIndices,
                    chosenCandidateIndices.Length,
                    assignment,
                    responseTimes,
                    responseTimes.Length,
                    mean, p50, p90,
                    withinFirst, withinSecond,
                    firstThreshold,
                    secondThreshold);

                if (status < 0)
                    throw new InvalidOperationException($"Native facility location kernel failed with status {status}.");

                result = new FacilityCoverageNativeResult
                {
                    Assignment = assignment,
                    ResponseTimes = responseTimes,
                    Mean = mean[0],
                    P50 = p50[0],
                    P90 = p90[0],
                    PercentWithinFirstThreshold = withinFirst[0],
                    PercentWithinSecondThreshold = withinSecond[0]
                };
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native coverage evaluation failed; falling back to managed implementation.");
                return false;
            }
        }

        private static bool IsNativeInvocationException(Exception exception)
        {
            return exception is DllNotFoundException
                or EntryPointNotFoundException
                or BadImageFormatException
                or SEHException
                or InvalidOperationException;
        }
    }
}
