using Portfolio.Common.DTOs;

namespace Portfolio.Services.Services.Fleet;

/// <summary>
/// Shapes solved routes into the public <see cref="OptimizeResultDto"/>.
/// </summary>
internal static class FleetResultBuilder
{
    internal static OptimizeResultDto BuildResult(
        OptimizeRequestDto request,
        SolverContext context,
        List<List<int>> routes,
        List<List<CoordinateDto>> paths,
        double[] iterationCosts,
        double matrixBuildMs,
        double solveMs,
        double pathExpandMs,
        bool nativeAccelerated)
    {
        var served = new HashSet<int>();
        var vehicleRoutes = new List<VehicleRouteDto>(routes.Count);
        var totalDistance = 0.0;
        var totalDuration = 0.0;
        var allFeasible = true;

        for (var r = 0; r < routes.Count; r++)
        {
            var route = routes[r];
            if (route.Count == 0)
                continue;

            var vehicleRoute = BuildVehicleRoute(
                request, context, route, r < paths.Count ? paths[r] : [], vehicleRoutes.Count, served);
            totalDistance += vehicleRoute.DistanceKm;
            totalDuration += vehicleRoute.DurationMinutes;
            allFeasible &= vehicleRoute.Feasible;
            vehicleRoutes.Add(vehicleRoute);
        }

        var unserved = FindUnserved(request, served);

        var initial = iterationCosts.Length > 0 ? iterationCosts[0] : 0;
        var final = iterationCosts.Length > 0 ? iterationCosts[^1] : 0;
        var improvement = initial > 0 ? (initial - final) / initial * 100.0 : 0;

        return new OptimizeResultDto
        {
            NativeAccelerated = nativeAccelerated,
            Feasible = allFeasible && unserved.Count == 0,
            Routes = vehicleRoutes,
            VehiclesUsed = vehicleRoutes.Count,
            TotalDistanceKm = totalDistance,
            TotalDurationMinutes = totalDuration,
            UnservedStopIds = unserved,
            InitialObjective = initial,
            FinalObjective = final,
            ImprovementPercent = improvement,
            IterationCosts = [.. iterationCosts],
            MatrixBuildMs = matrixBuildMs,
            SolveMs = solveMs,
            PathExpandMs = pathExpandMs
        };
    }

    private static VehicleRouteDto BuildVehicleRoute(
        OptimizeRequestDto request,
        SolverContext context,
        List<int> route,
        List<CoordinateDto> path,
        int vehicleIndex,
        HashSet<int> served)
    {
        var stopIds = new List<int>(route.Count);
        foreach (var stop in route)
        {
            served.Add(stop);
            stopIds.Add(request.Stops[stop].Id);
        }

        var (arrivals, returnTime, feasible) = SimulateRoute(context, route);

        var load = ManagedVrpSolver.RouteLoad(context, route);
        if (load > context.Capacity + ManagedVrpSolver.Epsilon)
            feasible = false;

        return new VehicleRouteDto
        {
            VehicleIndex = vehicleIndex,
            StopIds = stopIds,
            Path = path,
            DistanceKm = ManagedVrpSolver.RouteDistance(context, route),
            DurationMinutes = returnTime - context.ShiftStart,
            Load = load,
            ArrivalMinutes = arrivals,
            ReturnMinutes = returnTime,
            Feasible = feasible
        };
    }

    // Walks the route on the shift clock and records arrivals. Unlike the solver's feasibility test it keeps
    // going after a violation, so the result still reports times; an unreachable leg (infinite time) marks
    // the route infeasible.
    internal static (List<double> Arrivals, double ReturnTime, bool Feasible) SimulateRoute(
        SolverContext context,
        List<int> route)
    {
        var arrivals = new List<double>(route.Count);
        var t = context.ShiftStart;
        var prev = 0;
        var feasible = true;

        foreach (var stop in route)
        {
            t += context.LegTime(prev, context.Mi(stop));
            if (t < context.Ready[stop])
                t = context.Ready[stop];
            if (!double.IsFinite(t) || t > context.Due[stop] + ManagedVrpSolver.Epsilon)
                feasible = false;

            arrivals.Add(t);
            t += context.Service[stop];
            prev = context.Mi(stop);
        }

        t += context.LegTime(prev, 0);
        if (!double.IsFinite(t) || t > context.ShiftEnd + ManagedVrpSolver.Epsilon)
            feasible = false;

        return (arrivals, t, feasible);
    }

    private static List<int> FindUnserved(OptimizeRequestDto request, HashSet<int> served)
    {
        var unserved = new List<int>();
        for (var i = 0; i < request.Stops.Count; i++)
        {
            if (!served.Contains(i))
                unserved.Add(request.Stops[i].Id);
        }
        return unserved;
    }
}
