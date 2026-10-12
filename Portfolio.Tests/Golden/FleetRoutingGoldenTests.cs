using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Golden
{
    // Golden output for FleetRoutingService before it is split. The three shipped presets
    // are solved with the page's own fleet parameters and 1,000 iterations, and the
    // result is pinned: fleet size, distance, objective, every route's stop sequence and
    // the full improvement trace.
    //
    // The managed solver and the native vrp_solver_kernel are bit-identical, so these
    // values must hold with PORTFOLIO_DISABLE_NATIVE=1 and with the kernels present.
    public class FleetRoutingGoldenTests
    {
        private static FleetRoutingService NewService() =>
            new(new SpatialGraphService(NullLogger<SpatialGraphService>.Instance),
                NullLogger<FleetRoutingService>.Instance);

        private static async Task<(FleetScenarioDto Scenario, OptimizeResultDto Result)> SolvePresetAsync(string preset)
        {
            var service = NewService();
            var scenario = await service.GetScenarioAsync(preset);
            var result = await service.OptimizeAsync(new OptimizeRequestDto
            {
                DepotLatitude = scenario.DepotLatitude,
                DepotLongitude = scenario.DepotLongitude,
                Stops = scenario.Stops,
                VehicleCount = scenario.VehicleCount,
                VehicleCapacity = scenario.VehicleCapacity,
                ShiftStartMinutes = scenario.ShiftStartMinutes,
                ShiftEndMinutes = scenario.ShiftEndMinutes,
                VehicleFixedCost = scenario.VehicleFixedCost,
                MaxIterations = 1_000
            });
            return (scenario, result);
        }

        public record PresetGolden(
            string Preset,
            int StopCount,
            int FleetSize,
            int VehiclesUsed,
            double TotalDistanceKm,
            double FinalObjective,
            int[][] Routes,
            double[] IterationCosts);

        private static readonly PresetGolden Morning = new(
            "morning", 25, 3, 2, 35.043, 85.043,
            [
                [3, 23, 11, 10, 14, 13, 12, 9, 7, 8, 6, 5, 21],
                [20, 19, 24, 1, 25, 4, 22, 2, 15, 17, 18, 16]
            ],
            [85.892, 85.043, 85.043]);

        private static readonly PresetGolden FullDay = new(
            "fullday", 40, 5, 4, 61.381, 161.381,
            [
                [25, 40, 15, 38, 1, 26, 16, 35, 10, 33, 11],
                [27, 20, 19, 22, 21],
                [24, 39, 9, 32, 34, 12, 30, 31, 13, 14, 8, 7, 36, 18, 17],
                [28, 29, 5, 6, 2, 3, 37, 4, 23]
            ],
            [189.293, 166.085, 164.856, 163.473, 161.93, 161.498, 161.381, 161.381]);

        private static readonly PresetGolden Tight = new(
            "tight", 40, 6, 6, 76.999, 226.999,
            [
                [19, 18, 6, 37, 4, 3, 30],
                [5, 40],
                [34, 33, 32, 31, 7, 36, 35, 8, 9, 11],
                [26, 16, 14, 15, 10, 12, 13, 38, 39],
                [24, 23, 22, 27, 17, 28, 20, 21],
                [25, 29, 2, 1]
            ],
            [264.262, 235.085, 234.578, 233.536, 233.355, 233.233, 232.099, 230.954, 229.969, 229.918, 228.792, 228.014, 227.817, 226.999, 226.999]);

        public static TheoryData<string> PresetNames => ["morning", "fullday", "tight"];

        private static PresetGolden For(string preset) => preset switch
        {
            "morning" => Morning,
            "fullday" => FullDay,
            _ => Tight
        };

        [Theory]
        [MemberData(nameof(PresetNames))]
        public async Task Preset_SolvesToPinnedFleetDistanceAndObjective(string preset)
        {
            var golden = For(preset);

            var (scenario, result) = await SolvePresetAsync(preset);

            Assert.Equal(golden.StopCount, scenario.Stops.Count);
            Assert.Equal(golden.FleetSize, scenario.VehicleCount);
            Assert.True(result.Feasible);
            Assert.Empty(result.UnservedStopIds);
            Assert.Equal(golden.VehiclesUsed, result.VehiclesUsed);
            Assert.Equal(golden.TotalDistanceKm, result.TotalDistanceKm, 1e-2);
            Assert.Equal(golden.FinalObjective, result.FinalObjective, 1e-3);
        }

        [Theory]
        [MemberData(nameof(PresetNames))]
        public async Task Preset_RoutesAreTheExactPinnedStopSequences(string preset)
        {
            var golden = For(preset);

            var (_, result) = await SolvePresetAsync(preset);

            Assert.Equal(golden.Routes.Length, result.Routes.Count);
            for (var i = 0; i < golden.Routes.Length; i++)
            {
                Assert.Equal(golden.Routes[i], result.Routes[i].StopIds);
                Assert.True(result.Routes[i].Feasible);
            }

            // Every stop is served exactly once.
            var served = result.Routes.SelectMany(r => r.StopIds).Order().ToList();
            Assert.Equal(Enumerable.Range(1, golden.StopCount), served);
        }

        [Theory]
        [MemberData(nameof(PresetNames))]
        public async Task Preset_IterationCostsFollowThePinnedImprovementTrace(string preset)
        {
            var golden = For(preset);

            var (_, result) = await SolvePresetAsync(preset);

            Assert.Equal(golden.IterationCosts.Length, result.IterationCosts.Count);
            for (var i = 0; i < golden.IterationCosts.Length; i++)
                Assert.Equal(golden.IterationCosts[i], result.IterationCosts[i], 1e-3);

            Assert.Equal(golden.IterationCosts[0], result.InitialObjective, 1e-3);
            Assert.Equal(golden.FinalObjective, result.IterationCosts[^1], 1e-3);
        }

        // ── Validation ──────────────────────────────────────────────────────────

        private static readonly (double Lat, double Lon)[] Sites =
        [
            (34.0565, -117.1850),
            (34.0648, -117.1720),
            (34.0491, -117.2030),
            (34.0710, -117.1960),
            (34.0420, -117.1780),
            (34.0600, -117.2140),
            (34.0530, -117.1650),
            (34.0680, -117.2230)
        ];

        private static FleetStopDto Stop(int id, double demand = 100, double ready = 480, double due = 960, double service = 5) =>
            new()
            {
                Id = id,
                Label = $"Stop {id}",
                Latitude = Sites[(id - 1) % Sites.Length].Lat,
                Longitude = Sites[(id - 1) % Sites.Length].Lon,
                Demand = demand,
                ReadyMinutes = ready,
                DueMinutes = due,
                ServiceMinutes = service
            };

        private static OptimizeRequestDto ValidRequest() =>
            new()
            {
                DepotLatitude = 34.0620,
                DepotLongitude = -117.2255,
                Stops = [Stop(1), Stop(2), Stop(3)],
                VehicleCount = 2,
                VehicleCapacity = 500,
                ShiftStartMinutes = 480,
                ShiftEndMinutes = 960,
                VehicleFixedCost = 25,
                MaxIterations = 100
            };

        // Each row breaks exactly one rule. The messages become public 400 bodies.
        private static readonly (Action<OptimizeRequestDto> Break, string Message)[] InvalidCases =
        [
            (r => r.Stops = [], "At least one stop is required."),
            (r => r.Stops = [.. Enumerable.Range(1, 121).Select(i => Stop(i))], "Routing is limited to 120 stops."),
            (r => r.VehicleCount = 0, "Vehicle count must be between 1 and 20."),
            (r => r.VehicleCount = 21, "Vehicle count must be between 1 and 20."),
            (r => r.VehicleCapacity = 0, "Vehicle capacity must be greater than zero."),
            (r => r.VehicleCapacity = -5, "Vehicle capacity must be greater than zero."),
            (r => r.VehicleCapacity = double.NaN, "Vehicle capacity must be greater than zero."),
            (r => r.ShiftStartMinutes = 960, "Shift start must be earlier than shift end."),
            (r => r.ShiftEndMinutes = double.PositiveInfinity, "Shift start must be earlier than shift end."),
            (r => r.VehicleFixedCost = -1, "Vehicle fixed cost cannot be negative."),
            (r => r.VehicleFixedCost = double.NaN, "Vehicle fixed cost cannot be negative."),
            (r => r.MaxIterations = 0, "Iterations must be between 1 and 5000."),
            (r => r.MaxIterations = 5_001, "Iterations must be between 1 and 5000."),
            (r => r.DepotLatitude = double.NaN, "Stop coordinates must be finite values."),
            (r => r.DepotLongitude = double.PositiveInfinity, "Stop coordinates must be finite values."),
            (r => r.Stops[1].Latitude = double.NaN, "Stop coordinates must be finite values."),
            (r => r.Stops[1].Longitude = double.NegativeInfinity, "Stop coordinates must be finite values."),
            (r => r.Stops[2].Demand = 0, "Stop demand must be greater than zero."),
            (r => r.Stops[2].Demand = double.NaN, "Stop demand must be greater than zero."),
            (r => r.Stops[0].Demand = 501, "No vehicle can serve a stop whose demand exceeds capacity."),
            (r => { r.Stops[0].ReadyMinutes = 900; r.Stops[0].DueMinutes = 800; }, "Stop ready time must not be later than its due time."),
            (r => r.Stops[0].DueMinutes = double.NaN, "Stop ready time must not be later than its due time."),
            (r => r.Stops[0].ServiceMinutes = -1, "Stop service time cannot be negative."),
            (r => r.Stops[0].ServiceMinutes = double.NaN, "Stop service time cannot be negative.")
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
        public async Task Optimize_NullStopList_ThrowsArgumentException()
        {
            var request = ValidRequest();
            request.Stops = null!;

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => NewService().OptimizeAsync(request));

            Assert.StartsWith("At least one stop is required.", ex.Message);
        }

        [Fact]
        public async Task GetScenario_UnknownPreset_NamesTheShippedPresets()
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => NewService().GetScenarioAsync("nope"));

            Assert.StartsWith("Unknown scenario preset. Expected one of: ", ex.Message);
            Assert.Contains("morning", ex.Message);
            Assert.Contains("fullday", ex.Message);
            Assert.Contains("tight", ex.Message);
        }

        // ── TruncateToFleet ─────────────────────────────────────────────────────

        // Every stop weighs 400 against a capacity of 500, so no two stops can share a
        // truck: eight stops need eight routes. With two trucks, six stops must be
        // dropped and reported as unserved.
        [Fact]
        public async Task Optimize_MoreRoutesThanVehicles_KeepsTwoRoutesAndReportsTheRestUnserved()
        {
            var request = ValidRequest();
            request.Stops = [.. Enumerable.Range(1, 8).Select(i => Stop(i, demand: 400))];
            request.VehicleCount = 2;
            request.MaxIterations = 200;

            var result = await NewService().OptimizeAsync(request);

            Assert.Equal(TruncatedRouteStopIds.Length, result.Routes.Count);
            Assert.Equal(TruncatedRouteStopIds, result.Routes.Select(r => r.StopIds.Single()).ToArray());
            Assert.Equal(TruncatedUnservedStopIds, result.UnservedStopIds.Order().ToArray());
            Assert.False(result.Feasible);
            Assert.Equal(2, result.VehiclesUsed);

            // Kept and dropped stops partition the request.
            var all = result.Routes.SelectMany(r => r.StopIds).Concat(result.UnservedStopIds).Order().ToArray();
            Assert.Equal(Enumerable.Range(1, 8), all);
        }

        // The two kept routes are the shortest single-stop routes (ties broken by stop index).
        private static readonly int[] TruncatedRouteStopIds = [6, 8];
        private static readonly int[] TruncatedUnservedStopIds = [1, 2, 3, 4, 5, 7];
    }
}
