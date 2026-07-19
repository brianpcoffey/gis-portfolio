using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    // Fleet route optimization: capacitated vehicle routing with time windows over the
    // Redlands road network.
    //
    // The suite runs with no native shared library present, so every test exercises the
    // managed solver and asserts the computed result. The managed path mirrors
    // native/vrp_solver_kernel/src/vrp_solver_kernel.cpp move for move, so these also pin
    // the behaviour the native kernel must reproduce.
    //
    // Distances come from the real road graph rather than a hand-built matrix. The graph is
    // a compile-time constant, so the results are just as deterministic, and the tests cover
    // snapping and matrix construction as well as the solver itself.
    public class FleetRoutingServiceTests
    {
        // The scenario depot, on the industrial west side of the network.
        private const double DepotLat = 34.0620;
        private const double DepotLon = -117.2255;

        private static FleetRoutingService NewService() =>
            new(new SpatialGraphService(NullLogger<SpatialGraphService>.Instance),
                NullLogger<FleetRoutingService>.Instance);

        // Eight coordinates spread across the Redlands network; each snaps to a distinct
        // road node, so every stop is reachable from the depot and from every other stop.
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

        private static FleetStopDto Stop(
            int id,
            int site,
            double demand = 100,
            double ready = 480,
            double due = 960,
            double service = 5)
        {
            return new FleetStopDto
            {
                Id = id,
                Label = $"Stop {id}",
                Latitude = Sites[site % Sites.Length].Lat,
                Longitude = Sites[site % Sites.Length].Lon,
                Demand = demand,
                ReadyMinutes = ready,
                DueMinutes = due,
                ServiceMinutes = service
            };
        }

        private static OptimizeRequestDto Request(params FleetStopDto[] stops)
        {
            return new OptimizeRequestDto
            {
                DepotLatitude = DepotLat,
                DepotLongitude = DepotLon,
                Stops = [.. stops],
                VehicleCount = 4,
                VehicleCapacity = 500,
                ShiftStartMinutes = 480,
                ShiftEndMinutes = 960,
                VehicleFixedCost = 25,
                MaxIterations = 500
            };
        }

        // ── Construction and coverage ───────────────────────────────────────────

        [Fact]
        public async Task Optimize_SingleStop_ProducesOneRoute()
        {
            var service = NewService();

            var result = await service.OptimizeAsync(Request(Stop(1, 0)));

            Assert.True(result.Feasible);
            Assert.Equal(1, result.VehiclesUsed);
            var route = Assert.Single(result.Routes);
            Assert.Equal([1], route.StopIds);
            Assert.True(route.DistanceKm > 0);
        }

        [Fact]
        public async Task Optimize_AllStopsWithinCapacity_ServesEveryStop()
        {
            var service = NewService();
            var request = Request(Stop(1, 0), Stop(2, 1), Stop(3, 2), Stop(4, 3), Stop(5, 4));

            var result = await service.OptimizeAsync(request);

            Assert.Empty(result.UnservedStopIds);
            Assert.True(result.Feasible);
            Assert.Equal(5, result.Routes.Sum(r => r.StopIds.Count));
        }

        [Fact]
        public async Task Optimize_EveryStopAppearsExactlyOnce()
        {
            var service = NewService();
            var request = Request(Stop(1, 0), Stop(2, 1), Stop(3, 2), Stop(4, 3), Stop(5, 4), Stop(6, 5));

            var result = await service.OptimizeAsync(request);

            var served = result.Routes.SelectMany(r => r.StopIds).ToList();
            Assert.Equal(served.Count, served.Distinct().Count());
            Assert.Equal([1, 2, 3, 4, 5, 6], served.Order());
        }

        [Fact]
        public async Task Optimize_ReturnsToDepot()
        {
            var service = NewService();

            var result = await service.OptimizeAsync(Request(Stop(1, 0), Stop(2, 3)));

            var route = Assert.Single(result.Routes);
            Assert.True(route.Path.Count > 2);
            Assert.Equal(route.Path[0].X, route.Path[^1].X, 9);
            Assert.Equal(route.Path[0].Y, route.Path[^1].Y, 9);
            Assert.True(route.ReturnMinutes > route.ArrivalMinutes[^1]);
        }

        // ── Constraints ─────────────────────────────────────────────────────────

        [Fact]
        public async Task Optimize_LoadNeverExceedsCapacity()
        {
            var service = NewService();
            var request = Request(
                Stop(1, 0, demand: 200), Stop(2, 1, demand: 200), Stop(3, 2, demand: 200),
                Stop(4, 3, demand: 200), Stop(5, 4, demand: 200), Stop(6, 5, demand: 200));
            request.VehicleCapacity = 500;

            var result = await service.OptimizeAsync(request);

            Assert.All(result.Routes, r => Assert.True(r.Load <= request.VehicleCapacity));
            Assert.Empty(result.UnservedStopIds);
        }

        [Fact]
        public async Task Optimize_TightWindows_RespectsArrivalTimes()
        {
            var service = NewService();
            var request = Request(
                Stop(1, 0, ready: 500, due: 540),
                Stop(2, 1, ready: 560, due: 600),
                Stop(3, 2, ready: 620, due: 680),
                Stop(4, 3, ready: 700, due: 760));

            var result = await service.OptimizeAsync(request);

            var byId = request.Stops.ToDictionary(s => s.Id);
            foreach (var route in result.Routes)
            {
                for (var i = 0; i < route.StopIds.Count; i++)
                    Assert.True(route.ArrivalMinutes[i] <= byId[route.StopIds[i]].DueMinutes);
            }
        }

        [Fact]
        public async Task Optimize_EarlyArrival_WaitsUntilReady()
        {
            var service = NewService();
            // The depot is minutes away but nothing opens before 10:00, so every arrival has
            // to be pushed forward to its ready time.
            var request = Request(
                Stop(1, 0, ready: 600, due: 780),
                Stop(2, 1, ready: 620, due: 800),
                Stop(3, 2, ready: 640, due: 820));

            var result = await service.OptimizeAsync(request);

            var byId = request.Stops.ToDictionary(s => s.Id);
            foreach (var route in result.Routes)
            {
                for (var i = 0; i < route.StopIds.Count; i++)
                    Assert.True(route.ArrivalMinutes[i] >= byId[route.StopIds[i]].ReadyMinutes);
            }
        }

        [Fact]
        public async Task Optimize_ArrivalMinutesIsParallelToStopIds()
        {
            var service = NewService();

            var result = await service.OptimizeAsync(
                Request(Stop(1, 0), Stop(2, 1), Stop(3, 2), Stop(4, 3)));

            Assert.All(result.Routes, r => Assert.Equal(r.StopIds.Count, r.ArrivalMinutes.Count));
        }

        [Fact]
        public async Task Optimize_ImpossibleTimeWindow_MarksInfeasible()
        {
            var service = NewService();
            // A window that closes one minute after the shift opens, at a stop that is a
            // several-minute drive from the depot: no vehicle can ever make it.
            var request = Request(Stop(1, 4, ready: 480, due: 481));

            var result = await service.OptimizeAsync(request);

            Assert.False(result.Feasible);
            Assert.Equal([1], result.UnservedStopIds);
            Assert.Empty(result.Routes);
        }

        // ── Search behaviour ────────────────────────────────────────────────────

        [Fact]
        public async Task Optimize_FinalObjectiveNotWorseThanInitial()
        {
            var service = NewService();
            var request = Request(
                Stop(1, 0), Stop(2, 1), Stop(3, 2), Stop(4, 3),
                Stop(5, 4), Stop(6, 5), Stop(7, 6), Stop(8, 7));
            request.VehicleCapacity = 400;

            var result = await service.OptimizeAsync(request);

            Assert.True(result.FinalObjective <= result.InitialObjective + 1e-9);
            Assert.True(result.ImprovementPercent >= 0);
        }

        [Fact]
        public async Task Optimize_IterationCosts_AreNonIncreasing()
        {
            var service = NewService();
            var request = Request(
                Stop(1, 0), Stop(2, 1), Stop(3, 2), Stop(4, 3),
                Stop(5, 4), Stop(6, 5), Stop(7, 6), Stop(8, 7));
            request.VehicleCapacity = 400;

            var result = await service.OptimizeAsync(request);

            Assert.NotEmpty(result.IterationCosts);
            for (var i = 1; i < result.IterationCosts.Count; i++)
                Assert.True(result.IterationCosts[i] <= result.IterationCosts[i - 1] + 1e-9);
        }

        [Fact]
        public async Task Optimize_HigherVehicleFixedCost_UsesFewerVehicles()
        {
            var service = NewService();
            FleetStopDto[] Stops() =>
            [
                Stop(1, 0), Stop(2, 1), Stop(3, 2), Stop(4, 3),
                Stop(5, 4), Stop(6, 5), Stop(7, 6), Stop(8, 7)
            ];

            var cheap = Request(Stops());
            cheap.VehicleCapacity = 400;
            cheap.VehicleFixedCost = 0;

            var expensive = Request(Stops());
            expensive.VehicleCapacity = 400;
            expensive.VehicleFixedCost = 500;

            var cheapResult = await service.OptimizeAsync(cheap);
            var expensiveResult = await service.OptimizeAsync(expensive);

            Assert.True(expensiveResult.VehiclesUsed <= cheapResult.VehiclesUsed);
        }

        // ── Validation ──────────────────────────────────────────────────────────

        [Fact]
        public async Task Optimize_NullRequest_ThrowsArgumentNullException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.OptimizeAsync(null!));
        }

        [Fact]
        public async Task Optimize_NoStops_ThrowsArgumentException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentException>(() => service.OptimizeAsync(Request()));
        }

        [Fact]
        public async Task Optimize_TooManyStops_ThrowsArgumentException()
        {
            var service = NewService();
            var stops = Enumerable.Range(1, 121).Select(i => Stop(i, i)).ToArray();

            await Assert.ThrowsAsync<ArgumentException>(() => service.OptimizeAsync(Request(stops)));
        }

        [Fact]
        public async Task Optimize_ShiftEndBeforeStart_ThrowsArgumentException()
        {
            var service = NewService();
            var request = Request(Stop(1, 0));
            request.ShiftStartMinutes = 960;
            request.ShiftEndMinutes = 480;

            await Assert.ThrowsAsync<ArgumentException>(() => service.OptimizeAsync(request));
        }

        [Fact]
        public async Task Optimize_StopDemandExceedsCapacity_ThrowsArgumentException()
        {
            var service = NewService();
            var request = Request(Stop(1, 0, demand: 900));
            request.VehicleCapacity = 500;

            await Assert.ThrowsAsync<ArgumentException>(() => service.OptimizeAsync(request));
        }

        [Fact]
        public async Task Optimize_ReadyAfterDue_ThrowsArgumentException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentException>(
                () => service.OptimizeAsync(Request(Stop(1, 0, ready: 700, due: 600))));
        }

        [Fact]
        public async Task Optimize_VehicleCountOutOfRange_ThrowsArgumentException()
        {
            var service = NewService();
            var request = Request(Stop(1, 0));
            request.VehicleCount = 0;

            await Assert.ThrowsAsync<ArgumentException>(() => service.OptimizeAsync(request));
        }

        [Fact]
        public async Task Optimize_IterationsOutOfRange_ThrowsArgumentException()
        {
            var service = NewService();
            var request = Request(Stop(1, 0));
            request.MaxIterations = 5_001;

            await Assert.ThrowsAsync<ArgumentException>(() => service.OptimizeAsync(request));
        }

        // ── Scenarios ───────────────────────────────────────────────────────────

        [Fact]
        public async Task Scenario_UnknownPreset_ThrowsArgumentException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentException>(() => service.GetScenarioAsync("no-such-preset"));
        }

        [Fact]
        public async Task Scenario_IsDeterministic()
        {
            var service = NewService();

            var first = await service.GetScenarioAsync("fullday");
            var second = await service.GetScenarioAsync("fullday");

            Assert.Equal(first.Stops.Count, second.Stops.Count);
            for (var i = 0; i < first.Stops.Count; i++)
            {
                Assert.Equal(first.Stops[i].Latitude, second.Stops[i].Latitude);
                Assert.Equal(first.Stops[i].Longitude, second.Stops[i].Longitude);
                Assert.Equal(first.Stops[i].Demand, second.Stops[i].Demand);
                Assert.Equal(first.Stops[i].ReadyMinutes, second.Stops[i].ReadyMinutes);
                Assert.Equal(first.Stops[i].DueMinutes, second.Stops[i].DueMinutes);
            }
        }

        [Theory]
        [InlineData("morning")]
        [InlineData("fullday")]
        [InlineData("tight")]
        public async Task Scenario_EachPreset_IsInternallyConsistent(string preset)
        {
            var service = NewService();

            var scenario = await service.GetScenarioAsync(preset);

            Assert.NotEmpty(scenario.Stops);
            Assert.True(scenario.VehicleCount > 0);
            Assert.True(scenario.ShiftStartMinutes < scenario.ShiftEndMinutes);
            Assert.All(scenario.Stops, s =>
            {
                Assert.True(s.Demand > 0 && s.Demand <= scenario.VehicleCapacity);
                Assert.True(s.ReadyMinutes <= s.DueMinutes);
                Assert.True(s.ReadyMinutes >= scenario.ShiftStartMinutes);
                Assert.True(s.DueMinutes <= scenario.ShiftEndMinutes);
            });
            Assert.Equal(scenario.Stops.Count, scenario.Stops.Select(s => s.Id).Distinct().Count());
        }

        [Fact]
        public async Task Scenario_MorningPreset_SolvesFeasiblyWithSpareVehicles()
        {
            var service = NewService();
            var scenario = await service.GetScenarioAsync("morning");

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

            Assert.True(result.Feasible);
            Assert.Empty(result.UnservedStopIds);
            Assert.True(result.VehiclesUsed <= scenario.VehicleCount);
            Assert.All(result.Routes, r => Assert.NotEmpty(r.Path));
        }
    }
}
