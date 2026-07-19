using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    // Emergency response coverage: drive-time isochrones and p-median station siting.
    //
    // Optimization and isochrone tests drive the service through a stub graph service that
    // returns hand-built cost vectors, so the assertions pin the facility-location
    // arithmetic rather than the Redlands road network. Requests use AvgSpeedKmh = 60 so
    // one kilometre of graph cost is exactly one minute of travel time and the hand-built
    // numbers read directly.
    //
    // These tests assert computation, not which path produced it: they pass whether or
    // not the native shared libraries have been built. Native/managed equivalence is
    // covered by NativeParityTests and by `dotnet run --project Portfolio.Benchmarks`.
    // native/facility_location_kernel/src/facility_location_kernel.cpp line for line, so
    // these also pin the arithmetic the native kernel must reproduce.
    public class ResponseCoverageServiceTests
    {
        private const double SpeedForMinuteParity = 60.0;

        // ── Optimization ────────────────────────────────────────────────────────

        [Fact]
        public async Task Optimize_SingleCandidate_ChoosesIt()
        {
            var service = NewService(candidateCount: 1, demandCount: 3, costs: [1, 2, 3]);

            var result = await service.OptimizeAsync(Request(1, 3, stationCount: 1));

            Assert.Equal([1], result.ChosenCandidateIds);
            Assert.Equal(2.0, result.Optimized.MeanMinutes, 6);
        }

        [Fact]
        public async Task Optimize_TwoClusters_PlacesOneStationPerCluster()
        {
            // Candidate 1 sits on the left cluster, candidate 2 on the right, candidate 3
            // midway between them. Two stations should take the clusters, not the middle.
            var costs = new double[]
            {
                0, 0, 0, 0, 10, 10, 10, 10,   // candidate 1
                10, 10, 10, 10, 0, 0, 0, 0,   // candidate 2
                5, 5, 5, 5, 5, 5, 5, 5        // candidate 3
            };
            var service = NewService(candidateCount: 3, demandCount: 8, costs: costs);

            var result = await service.OptimizeAsync(Request(3, 8, stationCount: 2));

            Assert.Equal([1, 2], result.ChosenCandidateIds);
            Assert.Equal(0.0, result.Optimized.MeanMinutes, 6);
        }

        [Fact]
        public async Task Optimize_MoreStations_NeverWorsensObjective()
        {
            var costs = SpreadCosts(candidateCount: 6, demandCount: 12);
            var service = NewService(6, 12, costs);

            var previous = double.PositiveInfinity;
            for (var stations = 1; stations <= 5; stations++)
            {
                var result = await service.OptimizeAsync(Request(6, 12, stations));
                Assert.True(result.Optimized.MeanMinutes <= previous + 1e-9,
                    $"{stations} stations scored {result.Optimized.MeanMinutes}, worse than {previous}.");
                previous = result.Optimized.MeanMinutes;
            }
        }

        [Fact]
        public async Task Optimize_P90Objective_DiffersFromMeanObjective()
        {
            // Candidate 1 is excellent for five demand points and terrible for the sixth;
            // candidate 2 is mediocre for all six. The mean rewards candidate 1, the
            // 90th percentile — which is what NFPA 1710 measures — rewards candidate 2.
            var service = NewService(2, 6, SkewedCosts());

            var mean = await service.OptimizeAsync(Request(2, 6, stationCount: 1, objectiveMode: 0));
            var p90 = await service.OptimizeAsync(Request(2, 6, stationCount: 1, objectiveMode: 1));

            Assert.Equal([1], mean.ChosenCandidateIds);
            Assert.Equal([2], p90.ChosenCandidateIds);
            Assert.True(p90.Optimized.P90Minutes < mean.Optimized.P90Minutes);
            Assert.True(mean.Optimized.MeanMinutes < p90.Optimized.MeanMinutes);
        }

        [Fact]
        public async Task Optimize_WeightedP90_RespectsCallVolume()
        {
            // Nine light demand points sit one minute out and a single very heavy one sits
            // nine minutes out. The heavy point carries more than 10% of total call volume
            // on its own, so it sets the p90. The unweighted p90 of the same list is 1.
            var costs = new double[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 9 };
            var service = NewService(1, 10, costs);

            var request = Request(1, 10, stationCount: 1, objectiveMode: 1);
            for (var d = 0; d < 9; d++)
                request.DemandPoints[d].CallVolume = 1;
            request.DemandPoints[9].CallVolume = 100;

            var result = await service.OptimizeAsync(request);

            Assert.Equal(9.0, result.Optimized.P90Minutes, 6);
            Assert.NotEqual(1.0, result.Optimized.P90Minutes);
        }

        [Fact]
        public async Task Optimize_IterationObjectives_AreNonIncreasing()
        {
            var service = NewService(8, 20, SpreadCosts(8, 20));

            var result = await service.OptimizeAsync(Request(8, 20, stationCount: 3, objectiveMode: 1));

            Assert.NotEmpty(result.IterationObjectives);
            for (var i = 1; i < result.IterationObjectives.Count; i++)
                Assert.True(result.IterationObjectives[i] <= result.IterationObjectives[i - 1] + 1e-9);
        }

        [Fact]
        public async Task Optimize_AssignmentIsNearestChosenFacility()
        {
            const int candidateCount = 6;
            const int demandCount = 15;
            var costs = SpreadCosts(candidateCount, demandCount);
            var service = NewService(candidateCount, demandCount, costs);

            var result = await service.OptimizeAsync(Request(candidateCount, demandCount, stationCount: 3));

            var chosenIndices = result.ChosenCandidateIds.Select(id => id - 1).ToList();
            for (var d = 0; d < demandCount; d++)
            {
                var bestIndex = chosenIndices.OrderBy(c => costs[c * demandCount + d]).ThenBy(c => c).First();
                var assignment = result.Assignments[d];
                Assert.Equal(bestIndex + 1, assignment.AssignedCandidateId);
                Assert.Equal(costs[bestIndex * demandCount + d], assignment.ResponseMinutes, 3);
            }
        }

        [Fact]
        public async Task Optimize_MeetsNfpa_TrueWhenNinetyPercentWithinFirstThreshold()
        {
            // Nine of ten equally weighted demand points sit inside the four-minute
            // threshold: exactly 90%, which is the boundary NFPA 1710 is written at.
            var atBoundary = new double[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 9 };
            var justUnder = new double[] { 1, 1, 1, 1, 1, 1, 1, 1, 9, 9 };

            var boundaryResult = await NewService(1, 10, atBoundary)
                .OptimizeAsync(Request(1, 10, stationCount: 1));
            var underResult = await NewService(1, 10, justUnder)
                .OptimizeAsync(Request(1, 10, stationCount: 1));

            Assert.Equal(90.0, boundaryResult.Optimized.PercentWithinFirstThreshold, 6);
            Assert.True(boundaryResult.MeetsNfpa1710);

            Assert.Equal(80.0, underResult.Optimized.PercentWithinFirstThreshold, 6);
            Assert.False(underResult.MeetsNfpa1710);
        }

        [Fact]
        public async Task Optimize_CoverageMode_MaximizesDemandWithinThreshold()
        {
            // Candidate 1 minimises the mean but leaves one demand point seven minutes
            // out. Candidate 2 is slower on average yet brings every point inside four
            // minutes, which is what the maximum-covering objective is asked to find.
            var service = NewService(2, 6, SkewedCosts());

            var coverage = await service.OptimizeAsync(Request(2, 6, stationCount: 1, objectiveMode: 2));
            var mean = await service.OptimizeAsync(Request(2, 6, stationCount: 1, objectiveMode: 0));

            Assert.Equal([2], coverage.ChosenCandidateIds);
            Assert.Equal(100.0, coverage.Optimized.PercentWithinFirstThreshold, 6);
            Assert.Equal([1], mean.ChosenCandidateIds);
            Assert.True(mean.Optimized.PercentWithinFirstThreshold < 100.0);
        }

        [Fact]
        public async Task Optimize_BaselineReflectsExistingStations()
        {
            var costs = new double[]
            {
                9, 9, 9, 9,   // candidate 1 — today's station, poorly sited
                1, 1, 1, 1    // candidate 2 — the better site
            };
            var service = NewService(2, 4, costs);

            var request = Request(2, 4, stationCount: 1);
            request.Candidates[0].IsExisting = true;
            request.ExistingStationIds = [1];

            var result = await service.OptimizeAsync(request);

            Assert.Equal([2], result.ChosenCandidateIds);
            Assert.Equal(9.0, result.Baseline.P90Minutes, 6);
            Assert.Equal(1.0, result.Optimized.P90Minutes, 6);
            Assert.True(result.Optimized.P90Minutes < result.Baseline.P90Minutes);
        }

        [Fact]
        public async Task Optimize_StationCountExceedsCandidates_ThrowsArgumentException()
        {
            var service = NewService(2, 4, SpreadCosts(2, 4));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.OptimizeAsync(Request(2, 4, stationCount: 3)));
        }

        [Fact]
        public async Task Optimize_NullRequest_ThrowsArgumentNullException()
        {
            var service = NewService(1, 1, [1]);

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.OptimizeAsync(null!));
        }

        [Fact]
        public async Task Optimize_ZeroCallVolume_ThrowsArgumentException()
        {
            var service = NewService(1, 2, [1, 2]);

            var request = Request(1, 2, stationCount: 1);
            request.DemandPoints[1].CallVolume = 0;

            await Assert.ThrowsAsync<ArgumentException>(() => service.OptimizeAsync(request));
        }

        [Fact]
        public async Task Optimize_SpeedOutOfRange_ThrowsArgumentException()
        {
            var service = NewService(1, 2, [1, 2]);

            var request = Request(1, 2, stationCount: 1);
            request.AvgSpeedKmh = 400;

            await Assert.ThrowsAsync<ArgumentException>(() => service.OptimizeAsync(request));
        }

        // ── Isochrones ──────────────────────────────────────────────────────────

        [Fact]
        public async Task Isochrone_BandsPartitionReachableNodes()
        {
            // Costs in km; at 60 km/h these read as 0, 1, 3.5, 5, 9, 13 minutes.
            var service = NewIsochroneService([0, 1, 3.5, 5, 9, 13]);

            var result = await service.ComputeIsochroneAsync(IsochroneRequest([4, 8, 12]));

            Assert.Equal(6, result.ReachableNodes);
            Assert.Equal(6, result.Nodes.Count);
            Assert.Equal(result.Nodes.Count, result.Nodes.Select(n => n.NodeId).Distinct().Count());
            Assert.Equal(4, result.BandCounts.Count);
            Assert.Equal(result.ReachableNodes, result.BandCounts.Sum());
            Assert.Equal([3, 1, 1, 1], result.BandCounts);

            foreach (var node in result.Nodes)
            {
                Assert.InRange(node.BandIndex, 0, 3);
                var expected = node.Minutes <= 4 ? 0 : node.Minutes <= 8 ? 1 : node.Minutes <= 12 ? 2 : 3;
                Assert.Equal(expected, node.BandIndex);
            }
        }

        [Fact]
        public async Task Isochrone_OriginIsInFirstBand()
        {
            var service = NewIsochroneService([0, 2, 6]);

            var result = await service.ComputeIsochroneAsync(IsochroneRequest([4, 8, 12]));

            var origin = result.Nodes.Single(n => n.NodeId == 1);
            Assert.Equal(0.0, origin.Minutes);
            Assert.Equal(0, origin.BandIndex);
        }

        [Fact]
        public async Task Isochrone_UnreachableNodesExcluded()
        {
            var service = NewIsochroneService([0, 1, double.PositiveInfinity, 2, double.PositiveInfinity]);

            var result = await service.ComputeIsochroneAsync(IsochroneRequest([4, 8, 12]));

            Assert.Equal(3, result.ReachableNodes);
            Assert.Equal(2, result.UnreachableNodes);
            Assert.DoesNotContain(result.Nodes, n => n.NodeId is 3 or 5);
        }

        [Fact]
        public async Task Isochrone_HigherSpeed_ExpandsBands()
        {
            var costs = new double[] { 0, 1, 2, 3, 4, 5, 6, 7 };

            var slow = await NewIsochroneService(costs)
                .ComputeIsochroneAsync(IsochroneRequest([4, 8, 12], avgSpeedKmh: 20));
            var fast = await NewIsochroneService(costs)
                .ComputeIsochroneAsync(IsochroneRequest([4, 8, 12], avgSpeedKmh: 80));

            Assert.True(fast.BandCounts[0] > slow.BandCounts[0]);
            Assert.All(fast.Nodes, node =>
                Assert.True(node.Minutes <= slow.Nodes.Single(n => n.NodeId == node.NodeId).Minutes));
        }

        [Fact]
        public async Task Isochrone_UnsortedBands_ThrowsArgumentException()
        {
            var service = NewIsochroneService([0, 1, 2]);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.ComputeIsochroneAsync(IsochroneRequest([8, 4, 12])));
        }

        [Fact]
        public async Task Isochrone_UnknownOrigin_ThrowsArgumentException()
        {
            var service = NewIsochroneService([0, 1, 2]);

            var request = IsochroneRequest([4, 8, 12]);
            request.OriginNodeId = 9_999;

            await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeIsochroneAsync(request));
        }

        [Fact]
        public async Task Isochrone_NullRequest_ThrowsArgumentNullException()
        {
            var service = NewIsochroneService([0, 1, 2]);

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.ComputeIsochroneAsync(null!));
        }

        // ── Scenario dataset ────────────────────────────────────────────────────

        [Fact]
        public async Task Scenario_IsDeterministic()
        {
            var first = await NewRealService().GetScenarioAsync();
            var second = await NewRealService().GetScenarioAsync();

            Assert.Equal(first.TotalCallVolume, second.TotalCallVolume);
            Assert.Equal(first.DemandPoints.Count, second.DemandPoints.Count);
            Assert.Equal(
                first.DemandPoints.Select(d => (d.Id, d.NodeId, d.CallVolume)),
                second.DemandPoints.Select(d => (d.Id, d.NodeId, d.CallVolume)));
            Assert.Equal(
                first.Candidates.Select(c => (c.Id, c.NodeId, c.IsExisting)),
                second.Candidates.Select(c => (c.Id, c.NodeId, c.IsExisting)));
        }

        [Fact]
        public async Task Scenario_EveryLocationSnapsToRoadNode()
        {
            var graphService = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var service = new ResponseCoverageService(graphService, NullLogger<ResponseCoverageService>.Instance);

            var graph = await graphService.GetRedlandsGraphAsync();
            var scenario = await service.GetScenarioAsync();
            var nodeIds = graph.Nodes.ToDictionary(n => n.Id);

            Assert.NotEmpty(scenario.DemandPoints);
            Assert.NotEmpty(scenario.Candidates);
            Assert.Equal(2, scenario.ExistingStationIds.Count);

            Assert.All(scenario.DemandPoints, point =>
            {
                var node = nodeIds[point.NodeId];
                Assert.Equal(node.Latitude, point.Latitude);
                Assert.Equal(node.Longitude, point.Longitude);
                Assert.True(point.CallVolume > 0);
            });

            Assert.All(scenario.Candidates, candidate =>
            {
                var node = nodeIds[candidate.NodeId];
                Assert.Equal(node.Latitude, candidate.Latitude);
                Assert.Equal(node.Longitude, candidate.Longitude);
            });
        }

        // ── Fixtures ────────────────────────────────────────────────────────────

        // Candidate 1 is excellent for five demand points and terrible for the sixth;
        // candidate 2 is uniformly mediocre but inside the four-minute threshold. Used by
        // the objective-divergence and coverage-mode tests.
        private static double[] SkewedCosts() =>
        [
            1, 1, 1, 1, 1, 7,
            3.9, 3.9, 3.9, 3.9, 3.9, 3.9
        ];

        // A deterministic non-degenerate cost matrix: each candidate is close to one slice
        // of the demand and progressively farther from the rest.
        private static double[] SpreadCosts(int candidateCount, int demandCount)
        {
            var costs = new double[candidateCount * demandCount];
            for (var c = 0; c < candidateCount; c++)
            {
                for (var d = 0; d < demandCount; d++)
                {
                    var candidatePosition = (double)c / candidateCount;
                    var demandPosition = (double)d / demandCount;
                    costs[c * demandCount + d] = 0.5 + 12.0 * Math.Abs(candidatePosition - demandPosition);
                }
            }

            return costs;
        }

        private static ResponseCoverageService NewService(int candidateCount, int demandCount, double[] costs) =>
            new(new StubGraphService(costs, NodeCount(candidateCount, demandCount)),
                NullLogger<ResponseCoverageService>.Instance);

        private static ResponseCoverageService NewIsochroneService(double[] costs) =>
            new(new StubGraphService(costs, costs.Length), NullLogger<ResponseCoverageService>.Instance);

        private static ResponseCoverageService NewRealService() =>
            new(new SpatialGraphService(NullLogger<SpatialGraphService>.Instance),
                NullLogger<ResponseCoverageService>.Instance);

        private static int NodeCount(int candidateCount, int demandCount) => candidateCount + demandCount;

        // Demand points take node ids after the candidates so no id is shared.
        private static OptimizeCoverageRequestDto Request(
            int candidateCount,
            int demandCount,
            int stationCount,
            int objectiveMode = 0) => new()
            {
                Candidates = [.. Enumerable.Range(0, candidateCount).Select(c => new CandidateSiteDto
                {
                    Id = c + 1,
                    NodeId = c + 1,
                    Label = "Candidate " + (c + 1)
                })],
                DemandPoints = [.. Enumerable.Range(0, demandCount).Select(d => new DemandPointDto
                {
                    Id = d + 1,
                    NodeId = candidateCount + d + 1,
                    CallVolume = 1
                })],
                StationCount = stationCount,
                ObjectiveMode = objectiveMode,
                AvgSpeedKmh = SpeedForMinuteParity,
                FirstThresholdMinutes = 4,
                SecondThresholdMinutes = 8,
                MaxIterations = 50
            };

        private static IsochroneRequestDto IsochroneRequest(List<double> bands, double avgSpeedKmh = SpeedForMinuteParity) => new()
        {
            OriginNodeId = 1,
            AvgSpeedKmh = avgSpeedKmh,
            BandMinutes = bands
        };

        // Returns hand-built cost data instead of running Dijkstra, so the coverage tests
        // are independent of the Redlands road network. Node ids are 1..nodeCount.
        private sealed class StubGraphService : ISpatialGraphService
        {
            private readonly double[] _costs;
            private readonly RoadGraphDto _graph;

            public StubGraphService(double[] costs, int nodeCount)
            {
                _costs = costs;
                _graph = new RoadGraphDto
                {
                    Nodes = [.. Enumerable.Range(1, nodeCount).Select(id => new GraphNodeDto
                    {
                        Id = id,
                        Label = "Node " + id,
                        Latitude = 34.0 + id * 0.001,
                        Longitude = -117.0 + id * 0.001
                    })],
                    Edges = [new GraphEdgeDto { FromNodeId = 1, ToNodeId = nodeCount, Cost = 1, Bidirectional = true }],
                    DestinationNodeId = nodeCount
                };
            }

            public Task<RoadGraphDto> GetRedlandsGraphAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(_graph);

            public Task<double[]> ComputeDistancesAsync(RoadGraphDto graph, int originNodeId, CancellationToken cancellationToken = default) =>
                Task.FromResult(_costs);

            public Task<double[]> ComputeDistanceMatrixAsync(
                RoadGraphDto graph,
                IReadOnlyList<int> sourceIds,
                IReadOnlyList<int> targetIds,
                CancellationToken cancellationToken = default)
            {
                Assert.Equal(_costs.Length, sourceIds.Count * targetIds.Count);
                return Task.FromResult(_costs);
            }

            public int SnapToNearestNode(RoadGraphDto graph, double latitude, double longitude) => 1;

            public Task<RouteResultDto> FindShortestPathAsync(RouteRequestDto request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task<ServiceAreaResultDto> ComputeServiceAreaAsync(ServiceAreaRequestDto request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }
    }
}
