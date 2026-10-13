using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Data;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Native;
using Portfolio.Services.Services.Fleet;
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
        // Distance becomes travel minutes here, outside the graph search. Putting minutes on
        // graph edges would break A*'s haversine heuristic, which is admissible only because
        // edge costs are along-road kilometres.
        private const double AverageSpeedKmh = 40.0;

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
            FleetRequestValidator.Validate(request);

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

            var context = ManagedVrpSolver.BuildContext(request, costMatrix, timeMatrix, dim);

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
                (routes, iterationCosts) = ManagedVrpSolver.SolveManaged(context, request.VehicleCount, request.MaxIterations, cancellationToken);
                nativeAccelerated = false;
            }
            solveWatch.Stop();

            var pathWatch = Stopwatch.StartNew();
            var expanded = await ExpandRoutePathsAsync(graph, nodeIds, routes, cancellationToken);
            pathWatch.Stop();

            return FleetResultBuilder.BuildResult(
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

    }
}
