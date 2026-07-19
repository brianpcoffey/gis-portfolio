using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class VrpSolverNativeBridge
    {
        private static readonly bool _available;

        static VrpSolverNativeBridge()
        {
            try
            {
                _available = NativeLibrary.TryLoad(
                    "vrp_solver_kernel",
                    typeof(VrpSolverNativeBridge).Assembly,
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
                ? "Native VRP solver kernel loaded. Fleet optimization will use the C++ fast path."
                : "Native VRP solver kernel unavailable; using managed CVRPTW implementation.");
        }

        /// <summary>
        /// Solves the CVRPTW natively. <paramref name="routes"/> receives one list of
        /// zero-based stop indices per vehicle used; the depot is implicit at both ends.
        /// </summary>
        internal static bool TrySolveCvrptw(
            double[] costMatrix,
            double[] travelTimeMatrix,
            int matrixDim,
            VrpStopNative[] stops,
            double vehicleCapacity,
            int maxVehicles,
            double shiftStartMinutes,
            double shiftEndMinutes,
            double vehicleFixedCost,
            int maxIterations,
            ILogger logger,
            out List<List<int>>? routes,
            out double[]? iterationCosts)
        {
            routes = null;
            iterationCosts = null;
            if (!_available)
                return false;

            try
            {
                var routeStops = new int[stops.Length];
                var routeLengths = new int[maxVehicles];
                var costs = new double[maxIterations + 1];
                var iterationCount = new int[1];

                var routeCount = VrpSolverNativeInterop.SolveCvrptw(
                    costMatrix,
                    travelTimeMatrix,
                    matrixDim,
                    stops,
                    stops.Length,
                    vehicleCapacity,
                    maxVehicles,
                    shiftStartMinutes,
                    shiftEndMinutes,
                    vehicleFixedCost,
                    maxIterations,
                    routeStops,
                    routeStops.Length,
                    routeLengths,
                    routeLengths.Length,
                    costs,
                    costs.Length,
                    iterationCount);

                if (routeCount < 0)
                    throw new InvalidOperationException($"Native VRP solver kernel failed with status {routeCount}.");
                if (routeCount > routeLengths.Length)
                    throw new InvalidOperationException("Native VRP solver kernel reported more routes than vehicles.");

                var mapped = new List<List<int>>(routeCount);
                var cursor = 0;
                for (var r = 0; r < routeCount; r++)
                {
                    var length = routeLengths[r];
                    if (length < 0 || cursor + length > routeStops.Length)
                        throw new InvalidOperationException("Native VRP solver kernel returned an out-of-range route length.");

                    var route = new List<int>(length);
                    for (var k = 0; k < length; k++)
                    {
                        var stopIndex = routeStops[cursor + k];
                        if (stopIndex < 0 || stopIndex >= stops.Length)
                            throw new InvalidOperationException("Native VRP solver kernel returned an out-of-range stop index.");
                        route.Add(stopIndex);
                    }

                    cursor += length;
                    mapped.Add(route);
                }

                var written = iterationCount[0];
                if (written < 0 || written > costs.Length)
                    throw new InvalidOperationException("Native VRP solver kernel returned an out-of-range iteration count.");

                routes = mapped;
                iterationCosts = costs[..written];
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native CVRPTW solve failed; falling back to managed implementation.");
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
