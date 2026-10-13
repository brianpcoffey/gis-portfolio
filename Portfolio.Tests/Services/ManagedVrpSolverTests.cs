using Portfolio.Common.DTOs;
using Portfolio.Services.Services.Fleet;

namespace Portfolio.Tests.Services;

// Unit tests for the extracted managed solver and result builder on a hand-built line
// network: depot at 0 km, stop i at (i + 1) km, so distances are exact and obvious.
public class ManagedVrpSolverTests
{
    private const double Speed = 40.0;

    private static OptimizeRequestDto Request(int stops, double fixedCost = 10, double capacity = 1000)
    {
        var request = new OptimizeRequestDto
        {
            VehicleCapacity = capacity,
            VehicleFixedCost = fixedCost,
            ShiftStartMinutes = 480,
            ShiftEndMinutes = 960,
            VehicleCount = 5,
            MaxIterations = 100,
        };
        for (var i = 0; i < stops; i++)
        {
            request.Stops.Add(new FleetStopDto
            {
                Id = 100 + i,
                Demand = 10,
                ReadyMinutes = 480,
                DueMinutes = 960,
                ServiceMinutes = 5,
            });
        }
        return request;
    }

    private static SolverContext Context(OptimizeRequestDto request)
    {
        var dim = request.Stops.Count + 1;
        var cost = new double[dim * dim];
        var time = new double[dim * dim];
        for (var i = 0; i < dim; i++)
        {
            for (var j = 0; j < dim; j++)
            {
                cost[i * dim + j] = Math.Abs(i - j);
                time[i * dim + j] = Math.Abs(i - j) / Speed * 60.0;
            }
        }
        return ManagedVrpSolver.BuildContext(request, cost, time, dim);
    }

    [Fact]
    public void RouteDistance_AndLoad_FollowTheLine()
    {
        var context = Context(Request(3));

        Assert.Equal(0, ManagedVrpSolver.RouteDistance(context, []));
        Assert.Equal(6, ManagedVrpSolver.RouteDistance(context, [0, 1, 2]));
        Assert.Equal(6, ManagedVrpSolver.RouteDistance(context, [2, 1, 0]));
        Assert.Equal(20, ManagedVrpSolver.RouteLoad(context, [0, 2]));
    }

    [Fact]
    public void RouteFeasible_RejectsLateArrivalAndOverCapacity()
    {
        var request = Request(2);
        request.Stops[1].DueMinutes = 480; // 2 km away cannot be reached at shift start
        Assert.False(ManagedVrpSolver.RouteFeasible(Context(request), [1]));
        Assert.True(ManagedVrpSolver.RouteFeasible(Context(request), [0]));
        Assert.True(ManagedVrpSolver.RouteFeasible(Context(request), []));

        Assert.False(ManagedVrpSolver.RouteFeasible(Context(Request(2, capacity: 15)), [0, 1]));
    }

    [Fact]
    public void ConstructSavings_JoinsCollinearStopsIntoOneRoute()
    {
        var context = Context(Request(4));

        var routes = ManagedVrpSolver.ConstructSavings(context);

        var route = Assert.Single(routes);
        Assert.Equal([0, 1, 2, 3], route);
        Assert.Equal(8 + 10, ManagedVrpSolver.Objective(context, routes));
    }

    [Fact]
    public void ConstructSavings_SkipsStopThatCannotBeServedAlone()
    {
        var request = Request(3);
        request.Stops[1].DueMinutes = 480;
        var routes = ManagedVrpSolver.ConstructSavings(Context(request));

        Assert.DoesNotContain(routes, r => r.Contains(1));
    }

    [Fact]
    public void TwoOptPass_UntanglesRouteAndIsQuietAtLocalOptimum()
    {
        var context = Context(Request(4));
        var routes = new List<List<int>> { new() { 1, 3, 0, 2 } };
        var before = ManagedVrpSolver.RouteDistance(context, routes[0]);

        Assert.True(ManagedVrpSolver.TwoOptPass(context, routes));
        Assert.True(ManagedVrpSolver.RouteDistance(context, routes[0]) < before);
        Assert.False(ManagedVrpSolver.TwoOptPass(context, routes));
    }

    [Fact]
    public void OrOptMove_RelocatesWhenItPays_AndReturnsFalseAtOptimum()
    {
        var context = Context(Request(4));
        var routes = new List<List<int>> { new() { 0, 1, 2 }, new() { 3 } };
        var before = ManagedVrpSolver.Objective(context, routes);

        Assert.True(ManagedVrpSolver.OrOptMove(context, routes));
        Assert.True(ManagedVrpSolver.Objective(context, routes) < before);

        var optimal = new List<List<int>> { new() { 0, 1, 2, 3 } };
        Assert.False(ManagedVrpSolver.OrOptMove(context, optimal));
    }

    [Fact]
    public void OrOptMove_EmptyingARouteShedsItsFixedCost()
    {
        var context = Context(Request(2, fixedCost: 1000));
        var routes = new List<List<int>> { new() { 0 }, new() { 1 } };

        Assert.True(ManagedVrpSolver.OrOptMove(context, routes));
        ManagedVrpSolver.DropEmptyRoutes(routes);

        Assert.Single(routes);
    }

    [Fact]
    public void TruncateToFleet_KeepsLongestThenShortestRoutes()
    {
        var context = Context(Request(4));
        var routes = new List<List<int>> { new() { 3 }, new() { 1, 2 }, new() { 0 } };

        ManagedVrpSolver.TruncateToFleet(context, routes, 2);

        Assert.Equal(2, routes.Count);
        Assert.Equal([1, 2], routes[0]);
        Assert.Equal([0], routes[1]);
    }

    [Fact]
    public void SolveManaged_ObjectiveNeverIncreases_AndHonoursCancellation()
    {
        var context = Context(Request(6));

        var (routes, costs) = ManagedVrpSolver.SolveManaged(context, 5, 100, CancellationToken.None);

        Assert.NotEmpty(routes);
        for (var i = 1; i < costs.Length; i++)
            Assert.True(costs[i] <= costs[i - 1] + 1e-9);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => ManagedVrpSolver.SolveManaged(context, 5, 100, cts.Token));
    }

    [Fact]
    public void SimulateRoute_WaitsForReadyTimeAndFlagsLateReturn()
    {
        var request = Request(2);
        request.Stops[0].ReadyMinutes = 600;
        var context = Context(request);

        var (arrivals, returnTime, feasible) = FleetResultBuilder.SimulateRoute(context, [0, 1]);

        Assert.Equal(600, arrivals[0]);
        Assert.True(feasible);
        Assert.Equal(arrivals[1] + 5 + 2 / Speed * 60.0, returnTime, 9);

        request.ShiftEndMinutes = 601;
        Assert.False(FleetResultBuilder.SimulateRoute(Context(request), [0, 1]).Feasible);
    }

    [Fact]
    public void BuildResult_ReportsServedRoutesUnservedStopsAndImprovement()
    {
        var request = Request(3);
        var context = Context(request);
        List<List<int>> routes = [[0, 1], []];
        List<List<CoordinateDto>> paths = [[]];

        var result = FleetResultBuilder.BuildResult(request, context, routes, paths, [20, 15], 1, 2, 3, true);

        var route = Assert.Single(result.Routes);
        Assert.Equal([100, 101], route.StopIds);
        Assert.Equal(4, route.DistanceKm);
        Assert.Equal(20, route.Load);
        Assert.Equal([102], result.UnservedStopIds);
        Assert.False(result.Feasible);
        Assert.Equal(25, result.ImprovementPercent);
        Assert.True(result.NativeAccelerated);
        Assert.Equal(1, result.VehiclesUsed);
    }
}
