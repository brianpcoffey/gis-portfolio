using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Golden
{
    // Golden output for SpatialGraphService before it is split. Dijkstra and A* run over
    // the Redlands road network from fixed nodes to Esri HQ (node 2530). The result, the
    // number of nodes settled and the nodes themselves are pinned (in settle order for A*,
    // as a set for Dijkstra), so any change to the search shows up even when the final
    // path stays the same.
    //
    // These hold with PORTFOLIO_DISABLE_NATIVE=1 and with the native kernels present.
    //
    // Behaviour for edges that name an undeclared node is deliberately not pinned here.
    public class SpatialGraphGoldenTests
    {
        private const int EsriHq = 2530;

        private static SpatialGraphService NewService() =>
            new(NullLogger<SpatialGraphService>.Instance);

        // Order-sensitive checksum of an id sequence.
        private static long OrderHash(IEnumerable<int> ids)
        {
            long h = 17;
            foreach (var id in ids)
                h = unchecked(h * 31 + id);
            return h;
        }

        private static async Task<RouteResultDto> RouteAsync(SpatialGraphService service, int start, string algorithm)
        {
            var graph = await service.GetRedlandsGraphAsync();
            return await service.FindShortestPathAsync(new RouteRequestDto
            {
                Nodes = graph.Nodes,
                Edges = graph.Edges,
                StartNodeId = start,
                EndNodeId = EsriHq,
                Algorithm = algorithm
            });
        }

        public record RouteGolden(
            int Start,
            string Algorithm,
            double Cost,
            int NodeCount,
            long NodeHash,
            double Minutes,
            int Explored,
            long ExploredHash,
            int[] ExploredFirstTen);

        private static readonly RouteGolden[] Routes =
        [
            new(1, "dijkstra", 0.17, 2, 18898, 0.3, 6, 15119119790, [1, 3, 5, 6, 8, 2530]),
            new(1, "astar", 0.17, 2, 18898, 0.3, 2, 18898, [1, 2530]),
            new(844, "dijkstra", 0.98, 10, 36992067910487077, 1.5, 128, -281878735999826299, [844, 845, 842, 2210, 846, 847, 841, 2211, 2212, 849]),
            new(844, "astar", 0.98, 10, 36992067910487077, 1.5, 42, -6531297603459422292, [844, 842, 845, 843, 2215, 2216, 841, 2218, 2210, 846]),
            new(1687, "dijkstra", 4.13, 36, -2953830971648858178, 6.2, 1702, -3355693179759998294, [1687, 1690, 1691, 1689, 379, 1793, 1693, 378, 1795, 382]),
            new(1687, "astar", 4.13, 36, -2953830971648858178, 6.2, 337, 6949597625524133768, [1687, 1335, 1333, 1334, 1690, 1677, 1347, 1345, 1346, 1339]),
            new(2100, "dijkstra", 1.42, 15, 1462911314531019571, 2.1, 337, -8206470849781533754, [2100, 2099, 1118, 1110, 1117, 1096, 1113, 1100, 1095, 1088]),
            new(2100, "astar", 1.42, 15, 1462911314531019571, 2.1, 36, -3796793611130751844, [2100, 2099, 1118, 1117, 1113, 1110, 800, 1114, 1111, 799])
        ];

        public static TheoryData<int> RouteIndexes => [.. Enumerable.Range(0, Routes.Length)];

        [Theory]
        [MemberData(nameof(RouteIndexes))]
        public async Task Route_ToEsriHq_MatchesPinnedPathAndCost(int index)
        {
            var golden = Routes[index];

            var result = await RouteAsync(NewService(), golden.Start, golden.Algorithm);

            Assert.True(result.Found);
            Assert.Equal(golden.Algorithm, result.AlgorithmUsed);
            Assert.Equal(golden.Cost, result.TotalCost, 1e-6);
            Assert.Equal(golden.Cost, result.DistanceKm, 1e-6);
            Assert.Equal(golden.Minutes, result.EstimatedMinutes, 1e-6);
            Assert.Equal(golden.NodeCount, result.NodeIds.Count);
            Assert.Equal(golden.Start, result.NodeIds[0]);
            Assert.Equal(EsriHq, result.NodeIds[^1]);
            Assert.Equal(golden.NodeHash, OrderHash(result.NodeIds));
        }

        [Theory]
        [MemberData(nameof(RouteIndexes))]
        public async Task Route_ToEsriHq_SettlesThePinnedNodes(int index)
        {
            var golden = Routes[index];

            var result = await RouteAsync(NewService(), golden.Start, golden.Algorithm);

            Assert.Equal(golden.Explored, result.ExploredNodes);
            Assert.Equal(golden.Explored, result.ExploredNodeIds.Count);
            Assert.Equal(golden.ExploredFirstTen, result.ExploredNodeIds.Take(10));
            // Dijkstra settles equal-cost nodes in an order that differs between the managed
            // and native engines, so only the settled set is pinned for it. A* order is
            // identical in both.
            IEnumerable<int> settled = golden.Algorithm == "dijkstra" ? result.ExploredNodeIds.Order() : result.ExploredNodeIds;
            Assert.Equal(golden.ExploredHash, OrderHash(settled));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(844)]
        [InlineData(1687)]
        [InlineData(2100)]
        public async Task AStar_ExploresFewerNodesThanDijkstra_AndFindsTheSamePath(int start)
        {
            var service = NewService();

            var dijkstra = await RouteAsync(service, start, "dijkstra");
            var astar = await RouteAsync(service, start, "astar");

            Assert.True(astar.ExploredNodes < dijkstra.ExploredNodes);
            Assert.Equal(dijkstra.TotalCost, astar.TotalCost, 1e-9);
            Assert.Equal(dijkstra.NodeIds, astar.NodeIds);
        }

        [Fact]
        public async Task StartAtDestination_IsAZeroCostSingleNodePath()
        {
            var result = await RouteAsync(NewService(), EsriHq, "dijkstra");

            Assert.True(result.Found);
            Assert.Equal(0, result.TotalCost);
            Assert.Equal([EsriHq], result.NodeIds);
            Assert.Equal(1, result.ExploredNodes);
        }

        // ── Service area and distances ──────────────────────────────────────────

        [Fact]
        public async Task ServiceArea_ThreeKmFromEsriHq_ReachesThePinnedNodeCount()
        {
            var service = NewService();
            var graph = await service.GetRedlandsGraphAsync();

            var result = await service.ComputeServiceAreaAsync(new ServiceAreaRequestDto
            {
                Nodes = graph.Nodes,
                Edges = graph.Edges,
                OriginNodeId = EsriHq,
                MaxCost = 3
            });

            Assert.Equal(1244, result.ReachableNodeIds.Count);
            Assert.Equal(1513739L, result.ReachableNodeIds.Sum(id => (long)id));
            Assert.Contains(EsriHq, result.ReachableNodeIds);
        }

        [Fact]
        public async Task OneToAllDistances_FromEsriHq_MatchPinnedChecksum()
        {
            var service = NewService();
            var graph = await service.GetRedlandsGraphAsync();

            var distances = await service.ComputeDistancesAsync(graph, EsriHq);

            Assert.Equal(graph.Nodes.Count, distances.Length);
            Assert.All(distances, d => Assert.True(double.IsFinite(d)));
            Assert.Equal(7701.523, distances.Sum(), 1e-3);
            Assert.Equal(0, distances[graph.Nodes.FindIndex(n => n.Id == EsriHq)]);
        }

        // ── Immutability ────────────────────────────────────────────────────────

        // The graph is built once and shared by every request. Route, fleet and response
        // runs all read it; none may write to it.
        private static long GraphHash(RoadGraphDto graph)
        {
            long h = 17;
            foreach (var n in graph.Nodes)
            {
                h = unchecked(h * 31 + n.Id);
                h = unchecked(h * 31 + BitConverter.DoubleToInt64Bits(n.Latitude));
                h = unchecked(h * 31 + BitConverter.DoubleToInt64Bits(n.Longitude));
                h = unchecked(h * 31 + (n.Label?.GetHashCodeOrdinal() ?? 0));
            }
            foreach (var e in graph.Edges)
            {
                h = unchecked(h * 31 + e.FromNodeId);
                h = unchecked(h * 31 + e.ToNodeId);
                h = unchecked(h * 31 + BitConverter.DoubleToInt64Bits(e.Cost));
                h = unchecked(h * 31 + (e.Bidirectional ? 1 : 0));
            }
            return unchecked(h * 31 + graph.DestinationNodeId);
        }

        [Fact]
        public async Task SharedGraph_IsUnchanged_ByRouteFleetAndResponseRuns()
        {
            var graphService = NewService();
            var graph = await graphService.GetRedlandsGraphAsync();
            var before = GraphHash(graph);
            var nodeCount = graph.Nodes.Count;
            var edgeCount = graph.Edges.Count;

            await RouteAsync(graphService, 1687, "dijkstra");
            await RouteAsync(graphService, 1687, "astar");

            var fleet = new FleetRoutingService(graphService, NullLogger<FleetRoutingService>.Instance);
            var scenario = await fleet.GetScenarioAsync("morning");
            await fleet.OptimizeAsync(new OptimizeRequestDto
            {
                DepotLatitude = scenario.DepotLatitude,
                DepotLongitude = scenario.DepotLongitude,
                Stops = scenario.Stops,
                VehicleCount = scenario.VehicleCount,
                VehicleCapacity = scenario.VehicleCapacity,
                ShiftStartMinutes = scenario.ShiftStartMinutes,
                ShiftEndMinutes = scenario.ShiftEndMinutes,
                VehicleFixedCost = scenario.VehicleFixedCost,
                MaxIterations = 200
            });

            var response = new ResponseCoverageService(graphService, NullLogger<ResponseCoverageService>.Instance);
            var responseScenario = await response.GetScenarioAsync();
            await response.OptimizeAsync(new OptimizeCoverageRequestDto
            {
                DemandPoints = responseScenario.DemandPoints,
                Candidates = responseScenario.Candidates,
                StationCount = 3,
                ObjectiveMode = 1,
                MaxIterations = 20,
                ExistingStationIds = responseScenario.ExistingStationIds
            });

            var after = await graphService.GetRedlandsGraphAsync();
            Assert.Same(graph, after);
            Assert.Equal(nodeCount, after.Nodes.Count);
            Assert.Equal(edgeCount, after.Edges.Count);
            Assert.Equal(before, GraphHash(after));
        }

        [Fact]
        public async Task SharedGraph_IsTheSameInstanceAcrossServicesAndCalls()
        {
            var first = await NewService().GetRedlandsGraphAsync();
            var second = await NewService().GetRedlandsGraphAsync();
            var third = await NewService().GetRedlandsGraphAsync();

            Assert.Same(first, second);
            Assert.Same(second, third);
        }
    }

    internal static class OrdinalHashExtensions
    {
        // string.GetHashCode is randomised per process; this one is stable.
        public static int GetHashCodeOrdinal(this string value)
        {
            var h = 17;
            foreach (var c in value)
                h = unchecked(h * 31 + c);
            return h;
        }
    }
}
