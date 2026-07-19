using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Data;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Native;
using System.Diagnostics;

namespace Portfolio.Services.Services
{
    /// <summary>
    /// Capacitated vehicle routing with time windows over the real Redlands road network.
    ///
    /// The client sends only the scenario — depot, stops, fleet parameters. The graph is
    /// fetched server-side, the road-distance matrix is built once, and each solved leg is
    /// expanded back into a street-following polyline, so nothing large crosses the wire.
    /// </summary>
    public class FleetRoutingService : IFleetRoutingService
    {
        private const int MaxStops = 120;
        private const int MaxVehicles = 20;
        private const int MaxIterations = 5_000;

        // Distance becomes travel minutes here, outside the graph search. Putting minutes on
        // graph edges would break A*'s haversine heuristic, which is admissible only because
        // edge costs are along-road kilometres.
        private const double AverageSpeedKmh = 40.0;

        // Improvements smaller than this are floating-point dust, not progress. Mirrors
        // kEpsilon in native/vrp_solver_kernel/src/vrp_solver_kernel.cpp.
        private const double Epsilon = 1e-9;

        private readonly ISpatialGraphService _graphService;
        private readonly ILogger<FleetRoutingService> _logger;

        public FleetRoutingService(
            ISpatialGraphService graphService,
            ILogger<FleetRoutingService> logger)
        {
            _graphService = graphService;
            _logger = logger;
            VrpSolverNativeBridge.LogAvailability(_logger);
        }

        public async Task<FleetScenarioDto> GetScenarioAsync(
            string presetName,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(presetName);

            var graph = await _graphService.GetRedlandsGraphAsync(cancellationToken);
            var scenario = RedlandsDeliveryScenario.Build(graph, presetName);
            if (scenario is null)
            {
                throw new ArgumentException(
                    $"Unknown scenario preset. Expected one of: {string.Join(", ", RedlandsDeliveryScenario.PresetKeys)}.",
                    nameof(presetName));
            }

            return scenario;
        }

        public async Task<OptimizeResultDto> OptimizeAsync(
            OptimizeRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            Validate(request);

            cancellationToken.ThrowIfCancellationRequested();

            var graph = await _graphService.GetRedlandsGraphAsync(cancellationToken);

            var matrixWatch = Stopwatch.StartNew();
            var nodeIds = new int[request.Stops.Count + 1];
            nodeIds[0] = _graphService.SnapToNearestNode(graph, request.DepotLatitude, request.DepotLongitude);
            for (var i = 0; i < request.Stops.Count; i++)
            {
                nodeIds[i + 1] = _graphService.SnapToNearestNode(
                    graph, request.Stops[i].Latitude, request.Stops[i].Longitude);
            }

            var costMatrix = await _graphService.ComputeDistanceMatrixAsync(graph, nodeIds, nodeIds, cancellationToken);
            matrixWatch.Stop();

            var dim = nodeIds.Length;
            var timeMatrix = new double[costMatrix.Length];
            for (var i = 0; i < costMatrix.Length; i++)
                timeMatrix[i] = costMatrix[i] / AverageSpeedKmh * 60.0;

            var context = BuildContext(request, costMatrix, timeMatrix, dim);

            cancellationToken.ThrowIfCancellationRequested();

            var solveWatch = Stopwatch.StartNew();
            bool nativeAccelerated;
            List<List<int>> routes;
            double[] iterationCosts;

            if (VrpSolverNativeBridge.TrySolveCvrptw(
                costMatrix,
                timeMatrix,
                dim,
                MapStops(request.Stops),
                request.VehicleCapacity,
                request.VehicleCount,
                request.ShiftStartMinutes,
                request.ShiftEndMinutes,
                request.VehicleFixedCost,
                request.MaxIterations,
                _logger,
                out var nativeRoutes,
                out var nativeCosts))
            {
                routes = nativeRoutes!;
                iterationCosts = nativeCosts!;
                nativeAccelerated = true;
            }
            else
            {
                (routes, iterationCosts) = SolveManaged(context, request.VehicleCount, request.MaxIterations, cancellationToken);
                nativeAccelerated = false;
            }
            solveWatch.Stop();

            var pathWatch = Stopwatch.StartNew();
            var expanded = await ExpandRoutePathsAsync(graph, nodeIds, routes, cancellationToken);
            pathWatch.Stop();

            return BuildResult(
                request,
                context,
                routes,
                expanded,
                iterationCosts,
                matrixWatch.Elapsed.TotalMilliseconds,
                solveWatch.Elapsed.TotalMilliseconds,
                pathWatch.Elapsed.TotalMilliseconds,
                nativeAccelerated);
        }

        // ── Validation ──────────────────────────────────────────────────────────

        private static void Validate(OptimizeRequestDto request)
        {
            if (request.Stops is null || request.Stops.Count == 0)
                throw new ArgumentException("At least one stop is required.", nameof(request));
            if (request.Stops.Count > MaxStops)
                throw new ArgumentException($"Routing is limited to {MaxStops} stops.", nameof(request));
            if (request.VehicleCount < 1 || request.VehicleCount > MaxVehicles)
                throw new ArgumentException($"Vehicle count must be between 1 and {MaxVehicles}.", nameof(request));
            if (!IsFinite(request.VehicleCapacity) || request.VehicleCapacity <= 0)
                throw new ArgumentException("Vehicle capacity must be greater than zero.", nameof(request));
            if (!IsFinite(request.ShiftStartMinutes) || !IsFinite(request.ShiftEndMinutes) ||
                request.ShiftStartMinutes >= request.ShiftEndMinutes)
                throw new ArgumentException("Shift start must be earlier than shift end.", nameof(request));
            if (!IsFinite(request.VehicleFixedCost) || request.VehicleFixedCost < 0)
                throw new ArgumentException("Vehicle fixed cost cannot be negative.", nameof(request));
            if (request.MaxIterations < 1 || request.MaxIterations > MaxIterations)
                throw new ArgumentException($"Iterations must be between 1 and {MaxIterations}.", nameof(request));
            if (!IsFinite(request.DepotLatitude) || !IsFinite(request.DepotLongitude))
                throw new ArgumentException("Stop coordinates must be finite values.", nameof(request));

            foreach (var stop in request.Stops)
            {
                if (!IsFinite(stop.Latitude) || !IsFinite(stop.Longitude))
                    throw new ArgumentException("Stop coordinates must be finite values.", nameof(request));
                if (!IsFinite(stop.Demand) || stop.Demand <= 0)
                    throw new ArgumentException("Stop demand must be greater than zero.", nameof(request));
                if (stop.Demand > request.VehicleCapacity)
                    throw new ArgumentException("No vehicle can serve a stop whose demand exceeds capacity.", nameof(request));
                if (!IsFinite(stop.ReadyMinutes) || !IsFinite(stop.DueMinutes) || stop.ReadyMinutes > stop.DueMinutes)
                    throw new ArgumentException("Stop ready time must not be later than its due time.", nameof(request));
                if (!IsFinite(stop.ServiceMinutes) || stop.ServiceMinutes < 0)
                    throw new ArgumentException("Stop service time cannot be negative.", nameof(request));
            }
        }

        // ── Solver context ──────────────────────────────────────────────────────

        // Everything the objective and the feasibility walk need. Mirrors the Solver struct
        // in native/vrp_solver_kernel/src/vrp_solver_kernel.cpp.
        private sealed class SolverContext
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

        private static SolverContext BuildContext(
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

        private static VrpStopNative[] MapStops(List<FleetStopDto> stops)
        {
            var mapped = new VrpStopNative[stops.Count];
            for (var i = 0; i < stops.Count; i++)
            {
                mapped[i] = new VrpStopNative
                {
                    Demand = stops[i].Demand,
                    ReadyTime = stops[i].ReadyMinutes,
                    DueTime = stops[i].DueMinutes,
                    ServiceTime = stops[i].ServiceMinutes
                };
            }
            return mapped;
        }

        // ── Managed solver (mirrors the native kernel move for move) ────────────

        private static (List<List<int>> Routes, double[] IterationCosts) SolveManaged(
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

        private static double RouteDistance(SolverContext context, List<int> route)
        {
            if (route.Count == 0)
                return 0;

            var total = context.Leg(0, context.Mi(route[0]));
            for (var k = 1; k < route.Count; k++)
                total += context.Leg(context.Mi(route[k - 1]), context.Mi(route[k]));
            total += context.Leg(context.Mi(route[^1]), 0);
            return total;
        }

        private static double RouteLoad(SolverContext context, List<int> route)
        {
            var load = 0.0;
            for (var k = 0; k < route.Count; k++)
                load += context.Demand[route[k]];
            return load;
        }

        // Walks the route on the shift clock. Early arrival waits; late arrival fails. An
        // unreachable leg carries infinity, which fails the due-time test.
        private static bool RouteFeasible(SolverContext context, List<int> route)
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
                if (!IsFinite(t))
                    return false;
                if (t < context.Ready[stop])
                    t = context.Ready[stop];
                if (t > context.Due[stop] + Epsilon)
                    return false;
                t += context.Service[stop];
                prev = context.Mi(stop);
            }

            t += context.LegTime(prev, 0);
            return IsFinite(t) && t <= context.ShiftEnd + Epsilon;
        }

        // Total travel distance plus a fixed penalty per vehicle used. The penalty is what
        // makes the search prefer four trucks over five.
        private static double Objective(SolverContext context, List<List<int>> routes)
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

        private static void DropEmptyRoutes(List<List<int>> routes) => routes.RemoveAll(r => r.Count == 0);

        // Phase 1 — Clarke-Wright parallel savings.
        private static List<List<int>> ConstructSavings(SolverContext context)
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
                    if (!IsFinite(value))
                        continue;
                    savings.Add((value, i, j));
                }
            }

            // Descending savings, with the index pair as a total tiebreak so the merge order —
            // and therefore the whole solution — is deterministic.
            savings.Sort((a, b) =>
            {
                if (a.Value != b.Value)
                    return b.Value.CompareTo(a.Value);
                if (a.I != b.I)
                    return a.I.CompareTo(b.I);
                return a.J.CompareTo(b.J);
            });

            foreach (var (_, i, j) in savings)
            {
                var ri = routeOf[i];
                var rj = routeOf[j];
                if (ri < 0 || rj < 0 || ri == rj)
                    continue;

                var a = routes[ri];
                var b = routes[rj];
                if (a.Count == 0 || b.Count == 0)
                    continue;
                if (RouteLoad(context, a) + RouteLoad(context, b) > context.Capacity + Epsilon)
                    continue;

                List<int>? merged = null;

                // Only end-to-end joins are considered: reversing a leg is not free once time
                // windows are involved.
                if (a[^1] == i && b[0] == j)
                {
                    var candidate = new List<int>(a);
                    candidate.AddRange(b);
                    if (RouteFeasible(context, candidate))
                        merged = candidate;
                }
                if (merged is null && b[^1] == j && a[0] == i)
                {
                    var candidate = new List<int>(b);
                    candidate.AddRange(a);
                    if (RouteFeasible(context, candidate))
                        merged = candidate;
                }
                if (merged is null)
                    continue;

                routes[ri] = merged;
                routes[rj] = [];
                foreach (var stop in merged)
                    routeOf[stop] = ri;
            }

            DropEmptyRoutes(routes);
            return routes;
        }

        // More routes than trucks: keep the ones that serve the most stops, breaking ties on
        // the shorter route and then on stop index. The remainder become unserved. This runs
        // after local search, not before it — Or-opt consolidates routes, so truncating at
        // construction time would strand stops the search was about to absorb.
        private static void TruncateToFleet(SolverContext context, List<List<int>> routes, int maxVehicles)
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
        private static bool TwoOptPass(SolverContext context, List<List<int>> routes)
        {
            var anyImproved = false;

            for (var r = 0; r < routes.Count; r++)
            {
                var improved = true;
                while (improved)
                {
                    improved = false;
                    var m = routes[r].Count;
                    if (m < 3)
                        break;

                    var current = RouteDistance(context, routes[r]);
                    for (var i = 0; i + 1 < m && !improved; i++)
                    {
                        for (var j = i + 1; j < m && !improved; j++)
                        {
                            var candidate = new List<int>(routes[r]);
                            candidate.Reverse(i, j - i + 1);
                            if (RouteDistance(context, candidate) >= current - Epsilon)
                                continue;
                            if (!RouteFeasible(context, candidate))
                                continue;

                            routes[r] = candidate;
                            improved = true;
                            anyImproved = true;
                        }
                    }
                }
            }

            return anyImproved;
        }

        // Or-opt: relocate a run of 1-3 consecutive stops into any position of any route,
        // including its own. Emptying a route also sheds that vehicle's fixed cost, which is
        // how the fleet size shrinks.
        private static bool OrOptMove(SolverContext context, List<List<int>> routes)
        {
            for (var a = 0; a < routes.Count; a++)
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

                        for (var b = 0; b < routes.Count; b++)
                        {
                            var target = a == b ? trimmed : routes[b];
                            var targetDistance = a == b ? trimmedDistance : RouteDistance(context, routes[b]);

                            for (var ins = 0; ins <= target.Count; ins++)
                            {
                                var candidate = new List<int>(target);
                                candidate.InsertRange(ins, segment);

                                double delta;
                                if (a == b)
                                {
                                    delta = RouteDistance(context, candidate) - sourceDistance;
                                }
                                else
                                {
                                    delta = trimmedDistance + RouteDistance(context, candidate)
                                        - (sourceDistance + targetDistance);
                                    if (trimmed.Count == 0)
                                        delta -= context.FixedCost;
                                    if (target.Count == 0)
                                        delta += context.FixedCost;
                                }

                                if (delta >= -Epsilon)
                                    continue;
                                if (!RouteFeasible(context, candidate))
                                    continue;

                                if (a == b)
                                {
                                    routes[a] = candidate;
                                }
                                else
                                {
                                    routes[a] = trimmed;
                                    routes[b] = candidate;
                                }
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        // ── Road-following polylines ────────────────────────────────────────────

        // Expands each solved leg into a real street path, so the drawn routes trace actual
        // roads rather than straight lines between stops.
        private async Task<List<List<CoordinateDto>>> ExpandRoutePathsAsync(
            RoadGraphDto graph,
            int[] nodeIds,
            List<List<int>> routes,
            CancellationToken cancellationToken)
        {
            var expanded = new List<List<CoordinateDto>>(routes.Count);

            foreach (var route in routes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var path = new List<CoordinateDto>();
                if (route.Count == 0)
                {
                    expanded.Add(path);
                    continue;
                }

                var sequence = new List<int>(route.Count + 2) { nodeIds[0] };
                foreach (var stop in route)
                    sequence.Add(nodeIds[stop + 1]);
                sequence.Add(nodeIds[0]);

                for (var k = 0; k + 1 < sequence.Count; k++)
                {
                    if (sequence[k] == sequence[k + 1])
                        continue;

                    var leg = await _graphService.FindShortestPathAsync(new RouteRequestDto
                    {
                        Nodes = graph.Nodes,
                        Edges = graph.Edges,
                        StartNodeId = sequence[k],
                        EndNodeId = sequence[k + 1],
                        Algorithm = "astar"
                    }, cancellationToken);

                    if (!leg.Found || leg.Path.Count == 0)
                        continue;

                    var start = path.Count == 0 ? 0 : 1;
                    for (var p = start; p < leg.Path.Count; p++)
                        path.Add(leg.Path[p]);
                }

                expanded.Add(path);
            }

            return expanded;
        }

        // ── Result shaping ──────────────────────────────────────────────────────

        private static OptimizeResultDto BuildResult(
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

                var arrivals = new List<double>(route.Count);
                var stopIds = new List<int>(route.Count);
                var t = context.ShiftStart;
                var prev = 0;
                var feasible = true;

                foreach (var stop in route)
                {
                    served.Add(stop);
                    stopIds.Add(request.Stops[stop].Id);

                    t += context.LegTime(prev, context.Mi(stop));
                    if (t < context.Ready[stop])
                        t = context.Ready[stop];
                    if (!IsFinite(t) || t > context.Due[stop] + Epsilon)
                        feasible = false;

                    arrivals.Add(t);
                    t += context.Service[stop];
                    prev = context.Mi(stop);
                }

                t += context.LegTime(prev, 0);
                if (!IsFinite(t) || t > context.ShiftEnd + Epsilon)
                    feasible = false;

                var load = RouteLoad(context, route);
                if (load > context.Capacity + Epsilon)
                    feasible = false;

                var distance = RouteDistance(context, route);
                totalDistance += distance;
                totalDuration += t - context.ShiftStart;
                allFeasible &= feasible;

                vehicleRoutes.Add(new VehicleRouteDto
                {
                    VehicleIndex = vehicleRoutes.Count,
                    StopIds = stopIds,
                    Path = r < paths.Count ? paths[r] : [],
                    DistanceKm = distance,
                    DurationMinutes = t - context.ShiftStart,
                    Load = load,
                    ArrivalMinutes = arrivals,
                    ReturnMinutes = t,
                    Feasible = feasible
                });
            }

            var unserved = new List<int>();
            for (var i = 0; i < request.Stops.Count; i++)
            {
                if (!served.Contains(i))
                    unserved.Add(request.Stops[i].Id);
            }

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

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
