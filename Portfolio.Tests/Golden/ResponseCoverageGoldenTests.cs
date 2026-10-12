using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Golden
{
    // Golden output for ResponseCoverageService before it is split. The shipped scenario
    // (450 demand points, 24 candidate sites, stations 1 and 2 in service) is solved for
    // four new stations with the page's own parameters: 40 km/h, thresholds of 4 and
    // 8 minutes, 50 iterations. The three objective modes pick different sites.
    //
    // The managed solver mirrors the native facility_location_kernel, so these values must
    // hold with PORTFOLIO_DISABLE_NATIVE=1 and with the kernels present.
    public class ResponseCoverageGoldenTests
    {
        private const double StatsTolerance = 1e-2;
        private const double ObjectiveTolerance = 1e-3;

        private static ResponseCoverageService NewService() =>
            new(new SpatialGraphService(NullLogger<SpatialGraphService>.Instance),
                NullLogger<ResponseCoverageService>.Instance);

        private static async Task<OptimizeCoverageResultDto> SolveAsync(int mode)
        {
            var service = NewService();
            var scenario = await service.GetScenarioAsync();
            return await service.OptimizeAsync(new OptimizeCoverageRequestDto
            {
                DemandPoints = scenario.DemandPoints,
                Candidates = scenario.Candidates,
                StationCount = 4,
                ObjectiveMode = mode,
                AvgSpeedKmh = 40,
                FirstThresholdMinutes = 4,
                SecondThresholdMinutes = 8,
                MaxIterations = 50,
                ExistingStationIds = scenario.ExistingStationIds
            });
        }

        [Fact]
        public async Task Scenario_HasTheShippedShape()
        {
            var scenario = await NewService().GetScenarioAsync();

            Assert.Equal(450, scenario.DemandPoints.Count);
            Assert.Equal(24, scenario.Candidates.Count);
            Assert.Equal([1, 2], scenario.ExistingStationIds);
            Assert.Equal(20069.0, scenario.TotalCallVolume, 1e-6);
        }

        // Mode, chosen sites, then the optimized stats: mean, p50, p90, % within first and
        // second thresholds; then the improvement trace.
        public record ModeGolden(
            int Mode,
            int[] Chosen,
            double Mean,
            double P50,
            double P90,
            double Within4,
            double Within8,
            double[] IterationObjectives);

        private static readonly ModeGolden MeanMode = new(
            0, [1, 2, 3, 5], 1.406, 1.252, 2.424, 95.964, 100.0, [1.405876]);

        private static readonly ModeGolden P90Mode = new(
            1, [1, 2, 3, 4], 1.487, 1.257, 2.424, 94.056, 100.0, [3.573, 3.4065, 2.472, 2.424]);

        private static readonly ModeGolden CoverageMode = new(
            2, [8, 12, 13, 14], 2.513, 2.466, 3.574, 98.112, 100.0, [0.018885]);

        public static TheoryData<int> Modes => [0, 1, 2];

        private static ModeGolden For(int mode) => mode switch
        {
            0 => MeanMode,
            1 => P90Mode,
            _ => CoverageMode
        };

        [Theory]
        [MemberData(nameof(Modes))]
        public async Task FourStations_ChooseThePinnedSitesForEachObjective(int mode)
        {
            var golden = For(mode);

            var result = await SolveAsync(mode);

            Assert.Equal(golden.Chosen, result.ChosenCandidateIds);
            Assert.True(result.MeetsNfpa1710);
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public async Task FourStations_OptimizedStatsMatchThePinnedValues(int mode)
        {
            var golden = For(mode);

            var stats = (await SolveAsync(mode)).Optimized;

            Assert.Equal(golden.Mean, stats.MeanMinutes, StatsTolerance);
            Assert.Equal(golden.P50, stats.P50Minutes, StatsTolerance);
            Assert.Equal(golden.P90, stats.P90Minutes, StatsTolerance);
            Assert.Equal(golden.Within4, stats.PercentWithinFirstThreshold, StatsTolerance);
            Assert.Equal(golden.Within8, stats.PercentWithinSecondThreshold, StatsTolerance);
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public async Task FourStations_IterationObjectivesFollowThePinnedTrace(int mode)
        {
            var golden = For(mode);

            var objectives = (await SolveAsync(mode)).IterationObjectives;

            Assert.Equal(golden.IterationObjectives.Length, objectives.Count);
            for (var i = 0; i < golden.IterationObjectives.Length; i++)
                Assert.Equal(golden.IterationObjectives[i], objectives[i], ObjectiveTolerance);
        }

        // The stations in service today are the baseline every mode is compared with:
        // 76.4% of calls reached in four minutes, p90 of 4.68 minutes.
        [Theory]
        [MemberData(nameof(Modes))]
        public async Task Baseline_IsTheTwoExistingStations(int mode)
        {
            var baseline = (await SolveAsync(mode)).Baseline;

            Assert.Equal(2.479, baseline.MeanMinutes, StatsTolerance);
            Assert.Equal(1.802, baseline.P50Minutes, StatsTolerance);
            Assert.Equal(4.677, baseline.P90Minutes, StatsTolerance);
            Assert.Equal(76.396, baseline.PercentWithinFirstThreshold, StatsTolerance);
            Assert.Equal(100.0, baseline.PercentWithinSecondThreshold, StatsTolerance);
        }

        [Fact]
        public async Task P90Objective_BeatsMeanObjectiveOnP90_AndTheBaselineOnEverything()
        {
            var mean = await SolveAsync(0);
            var p90 = await SolveAsync(1);

            Assert.True(p90.Optimized.P90Minutes <= mean.Optimized.P90Minutes + 1e-9);
            Assert.True(p90.Optimized.P90Minutes < p90.Baseline.P90Minutes);
            Assert.True(p90.Optimized.PercentWithinFirstThreshold > p90.Baseline.PercentWithinFirstThreshold);
        }

        // ── Validation ──────────────────────────────────────────────────────────

        private static OptimizeCoverageRequestDto ValidRequest() =>
            new()
            {
                DemandPoints =
                [
                    new() { Id = 1, NodeId = 1, CallVolume = 10 },
                    new() { Id = 2, NodeId = 2, CallVolume = 20 }
                ],
                Candidates =
                [
                    new() { Id = 1, NodeId = 1, Label = "A" },
                    new() { Id = 2, NodeId = 2, Label = "B" }
                ],
                StationCount = 1,
                ObjectiveMode = 0,
                AvgSpeedKmh = 40,
                FirstThresholdMinutes = 4,
                SecondThresholdMinutes = 8,
                MaxIterations = 10
            };

        private static readonly (Action<OptimizeCoverageRequestDto> Break, string Message)[] InvalidCases =
        [
            (r => r.DemandPoints = [], "At least one demand point is required."),
            (r => r.DemandPoints = [.. Enumerable.Range(1, 2_001).Select(i => new DemandPointDto { Id = i, NodeId = 1, CallVolume = 1 })],
                "Coverage analysis is limited to 2000 demand points."),
            (r => r.DemandPoints[1].CallVolume = 0, "Call volume must be greater than zero."),
            (r => r.DemandPoints[1].CallVolume = double.NaN, "Call volume must be greater than zero."),
            (r => r.Candidates = [], "At least one candidate site is required."),
            (r => r.Candidates = [.. Enumerable.Range(1, 201).Select(i => new CandidateSiteDto { Id = i, NodeId = 1 })],
                "Coverage analysis is limited to 200 candidate sites."),
            (r => r.StationCount = 0, "Station count must be at least one."),
            (r => r.StationCount = 3, "Station count cannot exceed the number of candidate sites."),
            (r => r.AvgSpeedKmh = 4, "Average speed must be between 5 and 120 km/h."),
            (r => r.AvgSpeedKmh = 121, "Average speed must be between 5 and 120 km/h."),
            (r => r.AvgSpeedKmh = double.NaN, "Average speed must be between 5 and 120 km/h."),
            (r => r.ObjectiveMode = -1, "Objective mode must be 0 (mean), 1 (p90), or 2 (coverage)."),
            (r => r.ObjectiveMode = 3, "Objective mode must be 0 (mean), 1 (p90), or 2 (coverage)."),
            (r => r.FirstThresholdMinutes = 0, "The first response threshold must be greater than zero."),
            (r => r.FirstThresholdMinutes = double.NaN, "The first response threshold must be greater than zero."),
            (r => r.SecondThresholdMinutes = 3, "The second response threshold must be at least the first."),
            (r => r.SecondThresholdMinutes = double.PositiveInfinity, "The second response threshold must be at least the first.")
        ];

        public static TheoryData<int> InvalidCaseIndexes => [.. Enumerable.Range(0, InvalidCases.Length)];

        [Theory]
        [MemberData(nameof(InvalidCaseIndexes))]
        public async Task Optimize_InvalidRequest_ThrowsArgumentExceptionWithPinnedMessage(int index)
        {
            var (breakRule, message) = InvalidCases[index];
            var request = ValidRequest();
            breakRule(request);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => NewService().OptimizeAsync(request));

            Assert.StartsWith(message, ex.Message);
            Assert.Equal("request", ex.ParamName);
        }

        [Fact]
        public async Task Optimize_StationCountAboveTwentyFails_WithTheCandidateCountMessageFirst()
        {
            // 21 stations against 21 candidates passes the candidate check and reaches the cap.
            var request = ValidRequest();
            request.Candidates = [.. Enumerable.Range(1, 21).Select(i => new CandidateSiteDto { Id = i, NodeId = 1 })];
            request.StationCount = 21;

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => NewService().OptimizeAsync(request));

            Assert.StartsWith("Station count is limited to 20.", ex.Message);
        }

        public static TheoryData<int> BadBandCases => [0, 1, 2, 3, 4];

        [Theory]
        [MemberData(nameof(BadBandCases))]
        public async Task Isochrone_InvalidBands_ThrowsPinnedMessage(int index)
        {
            List<double>[] bands =
            [
                [],
                [2, 2],
                [5, 3],
                [0],
                [.. Enumerable.Range(1, 13).Select(i => (double)i)]
            ];

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => NewService().ComputeIsochroneAsync(
                new IsochroneRequestDto { OriginNodeId = 1, AvgSpeedKmh = 40, BandMinutes = bands[index] }));

            Assert.StartsWith("Isochrone bands must be ascending positive values.", ex.Message);
        }

        [Fact]
        public async Task Isochrone_UnknownOrigin_ThrowsPinnedMessage()
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => NewService().ComputeIsochroneAsync(
                new IsochroneRequestDto { OriginNodeId = -99, AvgSpeedKmh = 40, BandMinutes = [2, 4] }));

            Assert.StartsWith("The origin node was not found in the road network.", ex.Message);
        }
    }
}
