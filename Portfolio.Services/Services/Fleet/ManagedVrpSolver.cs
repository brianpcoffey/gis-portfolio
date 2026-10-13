using Portfolio.Common.DTOs;

namespace Portfolio.Services.Services.Fleet;

// Everything the objective and the feasibility walk need. Mirrors the Solver struct
// in native/vrp_solver_kernel/src/vrp_solver_kernel.cpp.
internal sealed class SolverContext
{
    public required double[] Cost { get; init; }
    public required double[] Time { get; init; }
    public required int Dim { get; init; }
    public required double[] Demand { get; init; }
    public required double[] Ready { get; init; }
    public required double[] Due { get; init; }
    public required double[] Service { get; init; }
    public required double Capacity { get; init; }
    public required double ShiftStart { get; init; }
    public required double ShiftEnd { get; init; }
    public required double FixedCost { get; init; }

    public int StopCount => Demand.Length;

    // Matrix index of a zero-based stop index. The depot occupies row/column 0.
    public int Mi(int stop) => stop + 1;

    public double Leg(int from, int to) => Cost[from * Dim + to];

    public double LegTime(int from, int to) => Time[from * Dim + to];
}

/// <summary>
/// The managed CVRPTW solver, mirroring native/vrp_solver_kernel move for move, so its iteration order
/// and first-improvement return points must not change.
/// </summary>
internal static class ManagedVrpSolver
{
    // Improvements smaller than this are floating-point dust, not progress. Mirrors
    // kEpsilon in native/vrp_solver_kernel/src/vrp_solver_kernel.cpp.
    internal const double Epsilon = 1e-9;

    internal static SolverContext BuildContext(
        OptimizeRequestDto request,
        double[] costMatrix,
        double[] timeMatrix,
        int dim)
    {
        var count = request.Stops.Count;
        var demand = new double[count];
        var ready = new double[count];
        var due = new double[count];
        var service = new double[count];

        for (var i = 0; i < count; i++)
        {
            demand[i] = request.Stops[i].Demand;
            ready[i] = request.Stops[i].ReadyMinutes;
            due[i] = request.Stops[i].DueMinutes;
            service[i] = request.Stops[i].ServiceMinutes;
        }

        return new SolverContext
        {
            Cost = costMatrix,
            Time = timeMatrix,
            Dim = dim,
            Demand = demand,
            Ready = ready,
            Due = due,
            Service = service,
            Capacity = request.VehicleCapacity,
            ShiftStart = request.ShiftStartMinutes,
            ShiftEnd = request.ShiftEndMinutes,
            FixedCost = request.VehicleFixedCost
        };
    }

    internal static (List<List<int>> Routes, double[] IterationCosts) SolveManaged(
        SolverContext context,
        int maxVehicles,
        int maxIterations,
        CancellationToken cancellationToken)
    {
        var routes = ConstructSavings(context);
        var costs = new List<double>(maxIterations + 1) { Objective(context, routes) };

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var improved = TwoOptPass(context, routes);
            if (OrOptMove(context, routes))
                improved = true;

            DropEmptyRoutes(routes);
            costs.Add(Objective(context, routes));

            if (!improved)
                break;
        }

        TruncateToFleet(context, routes, maxVehicles);
        return (routes, [.. costs]);
    }

    internal static double RouteDistance(SolverContext context, List<int> route)
    {
        if (route.Count == 0)
            return 0;

        var total = context.Leg(0, context.Mi(route[0]));
        for (var k = 1; k < route.Count; k++)
            total += context.Leg(context.Mi(route[k - 1]), context.Mi(route[k]));
        total += context.Leg(context.Mi(route[^1]), 0);
        return total;
    }

    internal static double RouteLoad(SolverContext context, List<int> route)
    {
        var load = 0.0;
        for (var k = 0; k < route.Count; k++)
            load += context.Demand[route[k]];
        return load;
    }

    // Walks the route on the shift clock. Early arrival waits; late arrival fails. An
    // unreachable leg carries infinity, which fails the due-time test.
    internal static bool RouteFeasible(SolverContext context, List<int> route)
    {
        if (route.Count == 0)
            return true;
        if (RouteLoad(context, route) > context.Capacity + Epsilon)
            return false;

        var t = context.ShiftStart;
        var prev = 0;
        for (var k = 0; k < route.Count; k++)
        {
            var stop = route[k];
            t += context.LegTime(prev, context.Mi(stop));
            if (!double.IsFinite(t))
                return false;
            if (t < context.Ready[stop])
                t = context.Ready[stop];
            if (t > context.Due[stop] + Epsilon)
                return false;
            t += context.Service[stop];
            prev = context.Mi(stop);
        }

        t += context.LegTime(prev, 0);
        return double.IsFinite(t) && t <= context.ShiftEnd + Epsilon;
    }

    // Total travel distance plus a fixed penalty per vehicle used. The penalty is what
    // makes the search prefer four trucks over five.
    internal static double Objective(SolverContext context, List<List<int>> routes)
    {
        var total = 0.0;
        foreach (var route in routes)
        {
            if (route.Count == 0)
                continue;
            total += RouteDistance(context, route) + context.FixedCost;
        }
        return total;
    }

    internal static void DropEmptyRoutes(List<List<int>> routes) => routes.RemoveAll(r => r.Count == 0);

    // Phase 1 — Clarke-Wright parallel savings.
    internal static List<List<int>> ConstructSavings(SolverContext context)
    {
        var (routes, routeOf) = SingletonRoutes(context);
        var savings = RankedSavings(context, routeOf);

        foreach (var (_, i, j) in savings)
            TryMergePair(context, routes, routeOf, i, j);

        DropEmptyRoutes(routes);
        return routes;
    }

    // One route per stop that can be served on its own; unservable stops get routeOf -1.
    private static (List<List<int>> Routes, int[] RouteOf) SingletonRoutes(SolverContext context)
    {
        var routes = new List<List<int>>();
        var routeOf = new int[context.StopCount];
        Array.Fill(routeOf, -1);

        for (var i = 0; i < context.StopCount; i++)
        {
            var single = new List<int> { i };
            if (!RouteFeasible(context, single))
                continue;
            routeOf[i] = routes.Count;
            routes.Add(single);
        }

        return (routes, routeOf);
    }

    private static List<(double Value, int I, int J)> RankedSavings(SolverContext context, int[] routeOf)
    {
        var savings = new List<(double Value, int I, int J)>();
        for (var i = 0; i < context.StopCount; i++)
        {
            if (routeOf[i] < 0)
                continue;
            for (var j = i + 1; j < context.StopCount; j++)
            {
                if (routeOf[j] < 0)
                    continue;
                var value = context.Leg(0, context.Mi(i)) + context.Leg(0, context.Mi(j))
                    - context.Leg(context.Mi(i), context.Mi(j));
                if (!double.IsFinite(value))
                    continue;
                savings.Add((value, i, j));
            }
        }

        // Descending savings, with the index pair as a total tiebreak so the merge order —
        // and therefore the whole solution — is deterministic.
        savings.Sort(CompareSavings);
        return savings;
    }

    private static int CompareSavings((double Value, int I, int J) a, (double Value, int I, int J) b)
    {
        if (a.Value != b.Value)
            return b.Value.CompareTo(a.Value);
        if (a.I != b.I)
            return a.I.CompareTo(b.I);
        return a.J.CompareTo(b.J);
    }

    private static void TryMergePair(
        SolverContext context,
        List<List<int>> routes,
        int[] routeOf,
        int i,
        int j)
    {
        var ri = routeOf[i];
        var rj = routeOf[j];
        if (ri < 0 || rj < 0 || ri == rj)
            return;

        var a = routes[ri];
        var b = routes[rj];
        if (a.Count == 0 || b.Count == 0)
            return;
        if (RouteLoad(context, a) + RouteLoad(context, b) > context.Capacity + Epsilon)
            return;

        var merged = TryMergeEnds(context, a, b, i, j);
        if (merged is null)
            return;

        routes[ri] = merged;
        routes[rj] = [];
        foreach (var stop in merged)
            routeOf[stop] = ri;
    }

    // Only end-to-end joins are considered: reversing a leg is not free once time
    // windows are involved.
    private static List<int>? TryMergeEnds(SolverContext context, List<int> a, List<int> b, int i, int j)
    {
        if (a[^1] == i && b[0] == j)
        {
            var candidate = new List<int>(a);
            candidate.AddRange(b);
            if (RouteFeasible(context, candidate))
                return candidate;
        }
        if (b[^1] == j && a[0] == i)
        {
            var candidate = new List<int>(b);
            candidate.AddRange(a);
            if (RouteFeasible(context, candidate))
                return candidate;
        }
        return null;
    }

    // More routes than trucks: keep the ones that serve the most stops, breaking ties on
    // the shorter route and then on stop index. The remainder become unserved. This runs
    // after local search, not before it — Or-opt consolidates routes, so truncating at
    // construction time would strand stops the search was about to absorb.
    internal static void TruncateToFleet(SolverContext context, List<List<int>> routes, int maxVehicles)
    {
        if (routes.Count <= maxVehicles)
            return;

        routes.Sort((x, y) =>
        {
            if (x.Count != y.Count)
                return y.Count.CompareTo(x.Count);
            var dx = RouteDistance(context, x);
            var dy = RouteDistance(context, y);
            if (dx != dy)
                return dx.CompareTo(dy);
            return x[0].CompareTo(y[0]);
        });
        routes.RemoveRange(maxVehicles, routes.Count - maxVehicles);
    }

    // 2-opt within one route: reverse route[i..j] and keep the reversal when it shortens
    // the route and stays time-feasible. First improvement, restarting after each move.
    internal static bool TwoOptPass(SolverContext context, List<List<int>> routes)
    {
        var anyImproved = false;

        for (var r = 0; r < routes.Count; r++)
        {
            if (TwoOptRoute(context, routes, r))
                anyImproved = true;
        }

        return anyImproved;
    }

    private static bool TwoOptRoute(SolverContext context, List<List<int>> routes, int r)
    {
        var anyImproved = false;

        while (true)
        {
            var m = routes[r].Count;
            if (m < 3)
                break;

            var current = RouteDistance(context, routes[r]);
            if (!TryReverseSegment(context, routes, r, m, current))
                break;
            anyImproved = true;
        }

        return anyImproved;
    }

    // Scans (i, j) in order and applies the first reversal that shortens the route.
    private static bool TryReverseSegment(
        SolverContext context,
        List<List<int>> routes,
        int r,
        int m,
        double current)
    {
        for (var i = 0; i + 1 < m; i++)
        {
            for (var j = i + 1; j < m; j++)
            {
                var candidate = new List<int>(routes[r]);
                candidate.Reverse(i, j - i + 1);
                if (RouteDistance(context, candidate) >= current - Epsilon)
                    continue;
                if (!RouteFeasible(context, candidate))
                    continue;

                routes[r] = candidate;
                return true;
            }
        }

        return false;
    }

    // Or-opt: relocate a run of 1-3 consecutive stops into any position of any route,
    // including its own. Emptying a route also sheds that vehicle's fixed cost, which is
    // how the fleet size shrinks.
    internal static bool OrOptMove(SolverContext context, List<List<int>> routes)
    {
        for (var a = 0; a < routes.Count; a++)
        {
            if (TryRelocateFromRoute(context, routes, a))
                return true;
        }

        return false;
    }

    private static bool TryRelocateFromRoute(SolverContext context, List<List<int>> routes, int a)
    {
        var source = routes[a];
        var sourceDistance = RouteDistance(context, source);

        for (var pos = 0; pos < source.Count; pos++)
        {
            for (var len = 1; len <= 3 && pos + len <= source.Count; len++)
            {
                var segment = source.GetRange(pos, len);

                var trimmed = new List<int>(source.Count - len);
                trimmed.AddRange(source.GetRange(0, pos));
                trimmed.AddRange(source.GetRange(pos + len, source.Count - pos - len));
                if (!RouteFeasible(context, trimmed))
                    continue;

                var trimmedDistance = RouteDistance(context, trimmed);

                if (TryRelocate(context, routes, a, segment, trimmed, sourceDistance, trimmedDistance))
                    return true;
            }
        }

        return false;
    }

    // Tries every insertion point of the segment in every route; applies the first
    // improving, feasible one.
    private static bool TryRelocate(
        SolverContext context,
        List<List<int>> routes,
        int a,
        List<int> segment,
        List<int> trimmed,
        double sourceDistance,
        double trimmedDistance)
    {
        for (var b = 0; b < routes.Count; b++)
        {
            var target = a == b ? trimmed : routes[b];
            var targetDistance = a == b ? trimmedDistance : RouteDistance(context, routes[b]);

            for (var ins = 0; ins <= target.Count; ins++)
            {
                var candidate = new List<int>(target);
                candidate.InsertRange(ins, segment);

                var delta = RelocationDelta(
                    context, a == b, candidate, trimmed, target, sourceDistance, trimmedDistance, targetDistance);

                if (delta >= -Epsilon || !RouteFeasible(context, candidate))
                    continue;

                ApplyRelocation(routes, a, b, trimmed, candidate);
                return true;
            }
        }

        return false;
    }

    private static void ApplyRelocation(
        List<List<int>> routes,
        int a,
        int b,
        List<int> trimmed,
        List<int> candidate)
    {
        if (a == b)
        {
            routes[a] = candidate;
        }
        else
        {
            routes[a] = trimmed;
            routes[b] = candidate;
        }
    }

    private static double RelocationDelta(
        SolverContext context,
        bool sameRoute,
        List<int> candidate,
        List<int> trimmed,
        List<int> target,
        double sourceDistance,
        double trimmedDistance,
        double targetDistance)
    {
        if (sameRoute)
            return RouteDistance(context, candidate) - sourceDistance;

        var delta = trimmedDistance + RouteDistance(context, candidate)
            - (sourceDistance + targetDistance);
        if (trimmed.Count == 0)
            delta -= context.FixedCost;
        if (target.Count == 0)
            delta += context.FixedCost;
        return delta;
    }
}
