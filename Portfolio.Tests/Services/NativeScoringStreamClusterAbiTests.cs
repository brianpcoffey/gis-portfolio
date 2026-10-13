using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Services.Native;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    // WP-53 part B: ABI contract tests for the scoring, geostream and cluster kernels.
    //
    // Like NativeParityTests, the tests that call a native entry point report Skipped when the
    // library is absent (the normal clean-clone state) and become real assertions when the kernels
    // are built (see native/README.md). The reference implementations below are deliberately
    // duplicated rather than reused from the services: they encode the contract the native kernels
    // must honour, so they must not drift if a service is refactored.
    public class NativeScoringStreamClusterAbiTests
    {
        private const int Unvisited = -2;
        private const int Noise = -1;

        // ---------------------------------------------------------------------------------------
        // The managed side of the float-to-int contract. The geostream kernel must reproduce this
        // exactly, so the test fails first if the runtime's conversion semantics ever change.
        // ---------------------------------------------------------------------------------------

        [Theory]
        [InlineData(double.NaN, 0)]
        [InlineData(double.PositiveInfinity, int.MaxValue)]
        [InlineData(double.NegativeInfinity, int.MinValue)]
        [InlineData(1e300, int.MaxValue)]
        [InlineData(-1e300, int.MinValue)]
        [InlineData(2147483648.0, int.MaxValue)]
        [InlineData(2147483647.0, int.MaxValue)]
        [InlineData(-2147483648.0, int.MinValue)]
        [InlineData(-2147483649.0, int.MinValue)]
        [InlineData(0.0, 0)]
        [InlineData(1234.0, 1234)]
        public void DotNetFloatToIntConversion_Saturates_AndMapsNaNToZero(double value, int expected)
        {
            Assert.Equal(expected, (int)Math.Floor(value));
        }

        // ---------------------------------------------------------------------------------------
        // geostream: status codes that already existed must keep their meaning.
        // ---------------------------------------------------------------------------------------

        private static TelemetryEventNative NativeEvent(double latitude, double longitude, double speed) => new()
        {
            EntityId = 1,
            Latitude = latitude,
            Longitude = longitude,
            SpeedMetersPerSecond = speed
        };

        private static GeoStreamOptionsNative NativeOptions(double grid) => new()
        {
            GridSizeDegrees = grid,
            AnomalySpeedThresholdMetersPerSecond = 30
        };

        [Fact]
        public void NativeGeoStream_InvalidArguments_ReturnMinusOneWithoutThrowing()
        {
            Assert.SkipUnless(GeoStreamNativeBridge.IsAvailable, "native library not loaded");

            var events = new[] { NativeEvent(34, -117, 10) };
            var options = NativeOptions(1);
            var aggregates = new GridAggregateNative[1];

            Assert.Equal(-1, GeoStreamNativeInterop.ProcessTelemetryBatch(null!, 1, in options, aggregates, 1, out _));
            Assert.Equal(-1, GeoStreamNativeInterop.ProcessTelemetryBatch(events, 1, in options, null!, 1, out _));
            Assert.Equal(-1, GeoStreamNativeInterop.ProcessTelemetryBatch(events, -1, in options, aggregates, 1, out _));
            Assert.Equal(-1, GeoStreamNativeInterop.ProcessTelemetryBatch(events, 1, in options, aggregates, -1, out _));

            var zeroGrid = NativeOptions(0);
            Assert.Equal(-1, GeoStreamNativeInterop.ProcessTelemetryBatch(events, 1, in zeroGrid, aggregates, 1, out _));
            var negativeGrid = NativeOptions(-1);
            Assert.Equal(-1, GeoStreamNativeInterop.ProcessTelemetryBatch(events, 1, in negativeGrid, aggregates, 1, out _));
        }

        [Fact]
        public void NativeGeoStream_AggregateBufferTooSmall_ReturnsMinusTwo()
        {
            Assert.SkipUnless(GeoStreamNativeBridge.IsAvailable, "native library not loaded");

            // Two events in two different one-degree cells need two aggregate slots.
            var events = new[] { NativeEvent(10.5, 10.5, 10), NativeEvent(20.5, 20.5, 10) };
            var options = NativeOptions(1);

            var status = GeoStreamNativeInterop.ProcessTelemetryBatch(events, 2, in options, new GridAggregateNative[1], 1, out _);

            Assert.Equal(-2, status);
        }

        // ---------------------------------------------------------------------------------------
        // geostream: native and the managed contract agree on in-range input.
        // ---------------------------------------------------------------------------------------

        private static List<TelemetryEventDto> SampleEvents(int count, int seed)
        {
            var rng = new Random(seed);
            var events = new List<TelemetryEventDto>(count);
            for (var i = 0; i < count; i++)
            {
                var nearHome = i % 3 == 0;
                events.Add(new TelemetryEventDto
                {
                    EntityId = i,
                    TimestampUtc = DateTime.UnixEpoch,
                    Latitude = nearHome ? 34.05 + (rng.NextDouble() - 0.5) * 0.1 : rng.NextDouble() * 200.0 - 100.0,
                    Longitude = nearHome ? -117.18 + (rng.NextDouble() - 0.5) * 0.1 : rng.NextDouble() * 400.0 - 200.0,
                    SpeedMetersPerSecond = rng.NextDouble() * 80.0
                });
            }

            return events;
        }

        private static bool IsInRange(TelemetryEventDto e)
        {
            return double.IsFinite(e.Latitude) && double.IsFinite(e.Longitude)
                && e.Latitude >= -90 && e.Latitude <= 90 && e.Longitude >= -180 && e.Longitude <= 180;
        }

        // The managed algorithm (GeoStreamProcessorService.ProcessManaged) with the .NET conversion
        // semantics, restated independently so it can be driven with grid sizes the service refuses.
        private static GeoStreamBatchResultDto ReferenceGeoStream(GeoStreamBatchRequestDto request)
        {
            var grid = request.GridSizeDegrees;
            var cells = new Dictionary<(int X, int Y), (int Count, double SpeedSum, double MaxSpeed, int Anomalies)>();
            var valid = 0;
            var anomalies = 0;
            foreach (var e in request.Events.Where(IsInRange))
            {
                valid++;
                var key = ((int)Math.Floor((e.Longitude + 180.0) / grid), (int)Math.Floor((e.Latitude + 90.0) / grid));
                cells.TryGetValue(key, out var cell);
                var anomaly = e.SpeedMetersPerSecond > request.AnomalySpeedThresholdMetersPerSecond;
                cells[key] = (cell.Count + 1, cell.SpeedSum + e.SpeedMetersPerSecond, Math.Max(cell.MaxSpeed, e.SpeedMetersPerSecond), cell.Anomalies + (anomaly ? 1 : 0));
                if (anomaly)
                    anomalies++;
            }

            return new GeoStreamBatchResultDto
            {
                TotalEvents = request.Events.Count,
                ValidEvents = valid,
                InvalidEvents = request.Events.Count - valid,
                AnomalyCount = anomalies,
                Aggregates = cells.Select(kvp => new GridAggregateDto
                {
                    CellX = kvp.Key.X,
                    CellY = kvp.Key.Y,
                    Count = kvp.Value.Count,
                    AverageSpeedMetersPerSecond = kvp.Value.SpeedSum / kvp.Value.Count,
                    MaxSpeedMetersPerSecond = kvp.Value.MaxSpeed,
                    AnomalyCount = kvp.Value.Anomalies,
                    CenterLatitude = (kvp.Key.Y + 0.5) * grid - 90.0,
                    CenterLongitude = (kvp.Key.X + 0.5) * grid - 180.0
                }).ToList()
            };
        }

        private static void AssertSameGeoStreamResult(GeoStreamBatchResultDto expected, GeoStreamBatchResultDto actual)
        {
            Assert.Equal(expected.TotalEvents, actual.TotalEvents);
            Assert.Equal(expected.ValidEvents, actual.ValidEvents);
            Assert.Equal(expected.InvalidEvents, actual.InvalidEvents);
            Assert.Equal(expected.AnomalyCount, actual.AnomalyCount);

            var want = expected.Aggregates.OrderBy(a => a.CellY).ThenBy(a => a.CellX).ToList();
            var got = actual.Aggregates.OrderBy(a => a.CellY).ThenBy(a => a.CellX).ToList();
            Assert.Equal(want.Count, got.Count);
            for (var i = 0; i < want.Count; i++)
            {
                Assert.Equal(want[i].CellX, got[i].CellX);
                Assert.Equal(want[i].CellY, got[i].CellY);
                Assert.Equal(want[i].Count, got[i].Count);
                Assert.Equal(want[i].AnomalyCount, got[i].AnomalyCount);
                Assert.Equal(want[i].AverageSpeedMetersPerSecond, got[i].AverageSpeedMetersPerSecond, 9);
                Assert.Equal(want[i].MaxSpeedMetersPerSecond, got[i].MaxSpeedMetersPerSecond, 9);
                Assert.Equal(want[i].CenterLatitude, got[i].CenterLatitude, 9);
                Assert.Equal(want[i].CenterLongitude, got[i].CenterLongitude, 9);
            }
        }

        [Theory]
        [InlineData(0.5)]
        [InlineData(0.01)]
        [InlineData(0.000001)]
        public void NativeGeoStream_InRangeBatch_MatchesManagedContract(double grid)
        {
            Assert.SkipUnless(GeoStreamNativeBridge.IsAvailable, "native library not loaded");

            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = grid,
                AnomalySpeedThresholdMetersPerSecond = 60,
                Events = SampleEvents(1500, 5302)
            };

            var native = GeoStreamNativeBridge.ProcessBatch(request);

            Assert.True(native.NativeAccelerated);
            AssertSameGeoStreamResult(ReferenceGeoStream(request), native);
        }

        // ---------------------------------------------------------------------------------------
        // geostream: cell boundaries. The kernel used to be built with fast math, which turns
        // (coordinate + offset) / grid into a multiplication by 1 / grid. The two differ in the last
        // bit, so a coordinate on a cell boundary landed one cell away from the managed service. The
        // kernel is built without fast math now and must give the managed cell for every one of them.
        // ---------------------------------------------------------------------------------------

        // latitude, longitude, grid, and the cell the managed service computes for them. The first
        // four are the cases a reciprocal multiplication gets wrong (it gives 516, 284, 1047 and
        // 4856999 for the first coordinate of each row below); the last is an ordinary point.
        public static TheoryData<double, double, double, int, int> BoundaryCells => new()
        {
            { 0.0, -128.4, 0.1, 515, 900 },
            { -61.6, 0.0, 0.1, 1800, 283 },
            { 0.0, -169.53, 0.01, 1046, 9000 },
            { 0.0, -175.143, 0.000001, 4857000, 90000000 },
            { 34.05, -117.18, 0.01, 6281, 12405 }
        };

        [Theory]
        [MemberData(nameof(BoundaryCells))]
        public void DotNetCellIndex_OnACellBoundary_IsTheIeeeQuotient(double latitude, double longitude, double grid, int cellX, int cellY)
        {
            Assert.Equal(cellX, (int)Math.Floor((longitude + 180.0) / grid));
            Assert.Equal(cellY, (int)Math.Floor((latitude + 90.0) / grid));
        }

        [Theory]
        [MemberData(nameof(BoundaryCells))]
        public void NativeGeoStream_CoordinateOnACellBoundary_LandsInTheManagedCell(double latitude, double longitude, double grid, int cellX, int cellY)
        {
            Assert.SkipUnless(GeoStreamNativeBridge.IsAvailable, "native library not loaded");

            var events = new[] { NativeEvent(latitude, longitude, 1) };
            var options = NativeOptions(grid);
            var aggregates = new GridAggregateNative[1];

            AssertStatus(0, () => GeoStreamNativeInterop.ProcessTelemetryBatch(events, 1, in options, aggregates, 1, out _));

            Assert.Equal((cellX, cellY), (aggregates[0].CellX, aggregates[0].CellY));
        }

        // Every longitude and every latitude from -180/-90 to 180/90 in steps of 0.001 degrees, which is
        // the kind of value a client sends, so a great many of them sit on a cell boundary at these grids
        // (reciprocal multiplication put 1,080 of the longitudes in the wrong cell at grid 0.1, 2,598 at
        // 0.01 and 22,108 at 1e-6). The native cells are compared with the managed ones as a histogram:
        // one event in the wrong cell changes it.
        [Theory]
        [InlineData(0.1)]
        [InlineData(0.01)]
        [InlineData(0.000001)]
        public void NativeGeoStream_SweepOfDecimalCoordinates_FillsTheManagedCells(double grid)
        {
            Assert.SkipUnless(GeoStreamNativeBridge.IsAvailable, "native library not loaded");

            const int stepsPerDegree = 1000;
            var events = new List<TelemetryEventNative>();
            for (var k = -180 * stepsPerDegree; k <= 180 * stepsPerDegree; k++)
                events.Add(NativeEvent(0.0, k / (double)stepsPerDegree, 1));
            for (var k = -90 * stepsPerDegree; k <= 90 * stepsPerDegree; k++)
                events.Add(NativeEvent(k / (double)stepsPerDegree, 0.0, 1));

            var expected = new Dictionary<(int CellX, int CellY), int>();
            foreach (var e in events)
            {
                var cell = ((int)Math.Floor((e.Longitude + 180.0) / grid), (int)Math.Floor((e.Latitude + 90.0) / grid));
                expected[cell] = expected.GetValueOrDefault(cell) + 1;
            }

            var eventArray = events.ToArray();
            var options = NativeOptions(grid);
            var aggregates = new GridAggregateNative[eventArray.Length];
            var result = new GeoStreamResultNative();

            AssertStatus(0, () => GeoStreamNativeInterop.ProcessTelemetryBatch(eventArray, eventArray.Length, in options, aggregates, aggregates.Length, out result));

            Assert.Equal(eventArray.Length, result.ValidEvents);
            var actual = aggregates.Take(result.AggregateCount).ToDictionary(a => (a.CellX, a.CellY), a => a.Count);
            var differing = expected.Keys.Union(actual.Keys).Count(cell => expected.GetValueOrDefault(cell) != actual.GetValueOrDefault(cell));
            Assert.True(differing == 0, $"{differing} grid cells differ from the managed cells at grid {grid}");
        }

        // ---------------------------------------------------------------------------------------
        // geostream: float-to-int edge values. The kernel used static_cast<int> on an unbounded
        // quotient, which is undefined behaviour in C++; it must now give the result .NET gives.
        // ---------------------------------------------------------------------------------------

        // Runs a native call that must return a status code and must not throw: no C++ exception
        // (or managed marshalling failure) may surface as an exception on this side of the boundary.
        private static void AssertStatus(int expected, Func<int> nativeCall)
        {
            var status = int.MinValue;
            var exception = Record.Exception(() => status = nativeCall());

            Assert.Null(exception);
            Assert.Equal(expected, status);
        }

        public static TheoryData<double> NonFiniteOrNonPositiveGrids => new()
        {
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity,
            0.0,
            -0.0,
            -1.0,
            -1e300
        };

        [Theory]
        [MemberData(nameof(NonFiniteOrNonPositiveGrids))]
        public void NativeGeoStream_GridThatIsNotFiniteAndPositive_ReturnsMinusOne(double grid)
        {
            Assert.SkipUnless(GeoStreamNativeBridge.IsAvailable, "native library not loaded");

            var events = new[] { NativeEvent(34, -117, 10) };
            var options = NativeOptions(grid);
            var aggregates = new GridAggregateNative[1];

            AssertStatus(-1, () => GeoStreamNativeInterop.ProcessTelemetryBatch(events, 1, in options, aggregates, 1, out _));
        }

        [Theory]
        [MemberData(nameof(NonFiniteOrNonPositiveGrids))]
        public void GeoStreamBridge_GridThatIsNotFiniteAndPositive_FallsBackInsteadOfThrowing(double grid)
        {
            Assert.SkipUnless(GeoStreamNativeBridge.IsAvailable, "native library not loaded");

            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = grid,
                Events = SampleEvents(10, 5304)
            };

            // The native -1 status becomes an InvalidOperationException inside the bridge, which the
            // Try... wrapper classifies as a native failure: it reports false so the service runs the
            // managed path, and the exception never reaches the caller.
            Assert.False(GeoStreamNativeBridge.TryProcessBatch(request, NullLogger.Instance, out var result));
            Assert.Null(result);

            var thrown = Assert.Throws<InvalidOperationException>(() => GeoStreamNativeBridge.ProcessBatch(request));
            Assert.Contains("status -1", thrown.Message);
        }

        // Grids far below the managed service minimum (1e-6), so the quotient overflows int32 for every
        // event but those on the lower edge (numerator 0) - or overflows double itself to +Infinity.
        // Denormal grids are left out on purpose: whether a denormal is flushed to zero depends on the
        // thread's floating-point mode, which is not part of this contract. (GCC before 13 linked
        // crtfastmath.o into a -ffast-math shared library, which set flush-to-zero for the loading
        // thread; GCC 13 and later do not do that for -shared, and this kernel no longer uses fast math.)
        public static TheoryData<double> SaturatingGrids => new()
        {
            1e-9,
            1e-12,
            1e-100,
            1e-300,
            2.2250738585072014E-308
        };

        [Theory]
        [MemberData(nameof(SaturatingGrids))]
        public void NativeGeoStream_GridTooSmallForInt32_SaturatesLikeDotNet(double grid)
        {
            Assert.SkipUnless(GeoStreamNativeBridge.IsAvailable, "native library not loaded");

            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = grid,
                AnomalySpeedThresholdMetersPerSecond = 30,
                Events =
                [
                    Event(-90, -180, 5),    // numerator 0: cell (0, 0) at any grid
                    Event(0, 0, 40),        // quotient 180/grid and 90/grid
                    Event(90, 180, 20),     // largest valid quotient
                    Event(34.05, -117.18, 31),
                    Event(34.05, -117.18, 10)
                ]
            };

            var native = GeoStreamNativeBridge.ProcessBatch(request);

            AssertSameGeoStreamResult(ReferenceGeoStream(request), native);
            Assert.Contains(native.Aggregates, a => a.CellX == int.MaxValue && a.CellY == int.MaxValue);
        }

        private static TelemetryEventDto Event(double latitude, double longitude, double speed) => new()
        {
            EntityId = 1,
            TimestampUtc = DateTime.UnixEpoch,
            Latitude = latitude,
            Longitude = longitude,
            SpeedMetersPerSecond = speed
        };

        [Theory]
        [InlineData(0.5)]
        [InlineData(1e-6)]
        [InlineData(1e-12)]
        public void NativeGeoStream_NonFiniteAndHugePositions_AreInvalidLikeManaged(double grid)
        {
            Assert.SkipUnless(GeoStreamNativeBridge.IsAvailable, "native library not loaded");

            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = grid,
                Events =
                [
                    Event(34.05, -117.18, 12),
                    Event(double.NaN, -117.18, 12),
                    Event(34.05, double.NaN, 12),
                    Event(double.PositiveInfinity, -117.18, 12),
                    Event(34.05, double.NegativeInfinity, 12),
                    Event(1e300, 0, 12),
                    Event(0, -1e300, 12),
                    Event(90.0000001, 0, 12),
                    Event(0, -180.0000001, 12),
                    Event(double.NaN, double.NaN, 12),
                    Event(-90, 180, 12)
                ]
            };

            var native = GeoStreamNativeBridge.ProcessBatch(request);

            Assert.Equal(11, native.TotalEvents);
            Assert.Equal(2, native.ValidEvents);
            Assert.Equal(9, native.InvalidEvents);
            AssertSameGeoStreamResult(ReferenceGeoStream(request), native);
        }

        // ---------------------------------------------------------------------------------------
        // cluster: status codes that already existed must keep their meaning.
        // ---------------------------------------------------------------------------------------

        [Fact]
        public void NativeCluster_InvalidArguments_ReturnMinusOneWithoutThrowing()
        {
            Assert.SkipUnless(SpatialClusterNativeBridge.IsAvailable, "native library not loaded");

            var points = new[] { new ClusterPointNative { X = 0, Y = 0 }, new ClusterPointNative { X = 1, Y = 1 } };
            var labels = new int[2];

            Assert.Equal(-1, SpatialClusterNativeInterop.RunDbscan(null!, 2, 1.0, 2, labels, 2));
            Assert.Equal(-1, SpatialClusterNativeInterop.RunDbscan(points, 2, 1.0, 2, null!, 2));
            Assert.Equal(-1, SpatialClusterNativeInterop.RunDbscan(points, 0, 1.0, 2, labels, 2));
            Assert.Equal(-1, SpatialClusterNativeInterop.RunDbscan(points, -1, 1.0, 2, labels, 2));
            Assert.Equal(-1, SpatialClusterNativeInterop.RunDbscan(points, 2, 1.0, 2, labels, 1));
            Assert.Equal(-1, SpatialClusterNativeInterop.RunDbscan(points, 2, 0.0, 2, labels, 2));
            Assert.Equal(-1, SpatialClusterNativeInterop.RunDbscan(points, 2, -1.0, 2, labels, 2));
            Assert.Equal(-1, SpatialClusterNativeInterop.RunDbscan(points, 2, 1.0, 0, labels, 2));
        }

        public static TheoryData<double> NonFiniteEpsilons => new()
        {
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity
        };

        [Theory]
        [MemberData(nameof(NonFiniteEpsilons))]
        public void NativeCluster_EpsilonThatIsNotFinite_ReturnsMinusOne(double epsilon)
        {
            Assert.SkipUnless(SpatialClusterNativeBridge.IsAvailable, "native library not loaded");

            var points = new[] { new ClusterPointNative { X = 0, Y = 0 }, new ClusterPointNative { X = 1, Y = 1 } };
            var labels = new int[2];

            AssertStatus(-1, () => SpatialClusterNativeInterop.RunDbscan(points, 2, epsilon, 2, labels, 2));
        }

        [Theory]
        [MemberData(nameof(NonFiniteEpsilons))]
        public void ClusterBridge_EpsilonThatIsNotFinite_FallsBackInsteadOfThrowing(double epsilon)
        {
            Assert.SkipUnless(SpatialClusterNativeBridge.IsAvailable, "native library not loaded");

            var request = new DbscanRequestDto { Epsilon = epsilon, MinPoints = 2, Points = DuplicateHeavyPoints() };

            // The native -1 status becomes an InvalidOperationException inside the bridge, which the
            // Try... wrapper classifies as a native failure: false, so the service runs the managed path.
            Assert.False(SpatialClusterNativeBridge.TryRunDbscan(request, NullLogger.Instance, out var labels, out var clusterCount));
            Assert.Null(labels);
            Assert.Equal(0, clusterCount);
        }

        // ---------------------------------------------------------------------------------------
        // cluster: DBSCAN labels must not depend on how (or whether) seeds are deduplicated.
        // ---------------------------------------------------------------------------------------

        // Textbook DBSCAN whose seed list may hold the same point many times. The managed service and
        // the native kernel both skip a point that is already queued for the current cluster; that is
        // only valid if it never changes a label, which is what these tests pin.
        private static int[] ReferenceDbscan(IReadOnlyList<ClusterPointDto> points, double epsilon, int minPoints)
        {
            var labels = new int[points.Count];
            Array.Fill(labels, Unvisited);
            var clusterId = 0;
            for (var p = 0; p < points.Count; p++)
            {
                if (labels[p] != Unvisited)
                    continue;

                var neighbors = ReferenceRegionQuery(points, p, epsilon);
                if (neighbors.Count < minPoints)
                {
                    labels[p] = Noise;
                    continue;
                }

                labels[p] = clusterId;
                ReferenceExpand(points, epsilon, minPoints, clusterId, labels, neighbors);
                clusterId++;
            }

            return labels;
        }

        private static void ReferenceExpand(IReadOnlyList<ClusterPointDto> points, double epsilon, int minPoints, int clusterId, int[] labels, List<int> seeds)
        {
            for (var s = 0; s < seeds.Count; s++)
            {
                var q = seeds[s];
                if (labels[q] == Noise)
                    labels[q] = clusterId;
                if (labels[q] != Unvisited)
                    continue;

                labels[q] = clusterId;
                var neighbors = ReferenceRegionQuery(points, q, epsilon);
                if (neighbors.Count >= minPoints)
                    seeds.AddRange(neighbors);
            }
        }

        private static List<int> ReferenceRegionQuery(IReadOnlyList<ClusterPointDto> points, int origin, double epsilon)
        {
            var epsilonSquared = epsilon * epsilon;
            var neighbors = new List<int>();
            for (var i = 0; i < points.Count; i++)
            {
                var dx = points[i].X - points[origin].X;
                var dy = points[i].Y - points[origin].Y;
                if (dx * dx + dy * dy <= epsilonSquared)
                    neighbors.Add(i);
            }

            return neighbors;
        }

        // Dense, overlapping blobs (every core point re-queues dozens of already queued neighbours),
        // a block of coincident points (all mutual neighbours), a chain, and scattered noise.
        private static List<ClusterPointDto> DuplicateHeavyPoints()
        {
            var rng = new Random(5303);
            var points = new List<ClusterPointDto>();
            foreach (var (cx, cy) in new[] { (0.0, 0.0), (0.7, 0.3), (6.0, 6.0) })
            {
                for (var i = 0; i < 200; i++)
                    points.Add(new ClusterPointDto { X = cx + rng.NextDouble() - 0.5, Y = cy + rng.NextDouble() - 0.5 });
            }

            for (var i = 0; i < 60; i++)
                points.Add(new ClusterPointDto { X = 12.0, Y = -3.0 });
            for (var i = 0; i < 40; i++)
                points.Add(new ClusterPointDto { X = 20.0 + i * 0.2, Y = 20.0 });
            for (var i = 0; i < 40; i++)
                points.Add(new ClusterPointDto { X = rng.NextDouble() * 60.0 - 30.0, Y = rng.NextDouble() * 60.0 - 30.0 });

            return points;
        }

        public static TheoryData<double, int> DbscanParameters => new()
        {
            { 0.3, 4 },
            { 0.5, 8 },
            { 0.15, 2 },
            { 0.05, 1 },
            { 1.0, 30 }
        };

        [Theory]
        [MemberData(nameof(DbscanParameters))]
        public async Task Dbscan_DuplicateHeavyInput_ServiceMatchesReferenceLabels(double epsilon, int minPoints)
        {
            var request = new DbscanRequestDto { Epsilon = epsilon, MinPoints = minPoints, Points = DuplicateHeavyPoints() };

            var result = await new SpatialClusterService(NullLogger<SpatialClusterService>.Instance).RunDbscanAsync(request);

            Assert.Equal(ReferenceDbscan(request.Points, epsilon, minPoints), result.Points.Select(p => p.ClusterId));
        }

        [Theory]
        [MemberData(nameof(DbscanParameters))]
        public void NativeDbscan_DuplicateHeavyInput_MatchesReferenceLabels(double epsilon, int minPoints)
        {
            Assert.SkipUnless(SpatialClusterNativeBridge.IsAvailable, "native library not loaded");

            var request = new DbscanRequestDto { Epsilon = epsilon, MinPoints = minPoints, Points = DuplicateHeavyPoints() };
            var expected = ReferenceDbscan(request.Points, epsilon, minPoints);

            Assert.True(SpatialClusterNativeBridge.TryRunDbscan(request, NullLogger.Instance, out var labels, out var clusterCount));

            Assert.Equal(expected, labels);
            Assert.Equal(Math.Max(0, expected.Max() + 1), clusterCount);
        }

        // Every point is within epsilon of every other. Before the kernel deduplicated seeds, each core
        // point re-queued all of its neighbours, so the work list grew by `count` per point: 12,000
        // coincident points needed about 1 GB. The bound itself is not asserted here (process memory is
        // too noisy for a unit test); this pins the labels on the input that exposed it.
        [Fact]
        public void NativeDbscan_ManyCoincidentPoints_FormOneClusterAndLeaveOutliersAsNoise()
        {
            Assert.SkipUnless(SpatialClusterNativeBridge.IsAvailable, "native library not loaded");

            var points = Enumerable.Range(0, 4000).Select(_ => new ClusterPointDto { X = 1.0, Y = 1.0 }).ToList();
            points.Add(new ClusterPointDto { X = 500.0, Y = 500.0 });
            var request = new DbscanRequestDto { Epsilon = 0.5, MinPoints = 3, Points = points };

            Assert.True(SpatialClusterNativeBridge.TryRunDbscan(request, NullLogger.Instance, out var labels, out var clusterCount));

            Assert.Equal(1, clusterCount);
            Assert.All(labels!.Take(4000), label => Assert.Equal(0, label));
            Assert.Equal(Noise, labels![4000]);
        }

        // ---------------------------------------------------------------------------------------
        // scoring: status codes, output length and wrapping integer arithmetic. ScoreProperty and
        // ScorePropertyBatch used to return void, take no output length and have no try/catch.
        // ---------------------------------------------------------------------------------------

        private static PropertyInputNative ScoringProperty() => new()
        {
            Price = 650_000,
            PropertyTax = 8_500,
            HoaFee = 200,
            Utilities = 150,
            LotSqft = 2_400,
            Bedrooms = 4,
            Bathrooms = 2,
            CommuteMin = 25,
            SchoolRating = 80,
            Walkability = 70,
            TransitAccess = 60,
            CrimeScore = 20,
            FutureAppreciation = 70,
            ResalePotential = 65,
            RoofCondition = 85,
            AcCondition = 80,
            PlumbingCondition = 90,
            ElectricalCondition = 88,
            LastRenovationYear = 2021,
            AmenitiesScore = 75,
            FloodRisk = 10,
            NoiseLevel = 30,
            Latitude = 34.05,
            Longitude = -117.18
        };

        private static PreferencesInputNative ScoringPrefs() => new()
        {
            MaxPrice = 800_000,
            MaxMonthlyBudget = 4_500,
            MinBedrooms = 3,
            MinSqft = 1_800,
            MaxCommuteMin = 45,
            WAffordability = 0.20,
            WNeighborhood = 0.15,
            WSize = 0.10,
            WAppreciation = 0.10,
            WCondition = 0.10,
            WCommute = 0.10,
            WAmenities = 0.10,
            WTaxUtilities = 0.05,
            WResale = 0.05,
            WEnvironment = 0.05,
            CurrentYear = 2026
        };

        private static ScoreOutputNative Sentinel() => new() { Composite = -7.0, Size = -7.0 };

        private static ScoreOutputNative[] SentinelBuffer(int length)
        {
            var buffer = new ScoreOutputNative[length];
            Array.Fill(buffer, Sentinel());
            return buffer;
        }

        [Fact]
        public void NativeScoring_BatchAndSingle_ReturnOkAndAgree()
        {
            Assert.SkipUnless(NativeScoringBridge.IsAvailable, "native library not loaded");

            var first = ScoringProperty();
            var second = first with { Price = 500_000, Bedrooms = 2 };
            var prefs = ScoringPrefs();
            var scores = new ScoreOutputNative[2];

            AssertStatus(0, () => NativeScoringInterop.ScorePropertyBatch([first, second], 2, in prefs, scores, 2));

            var single = new ScoreOutputNative();
            AssertStatus(0, () => NativeScoringInterop.ScoreProperty(in first, in prefs, out single, 1));
            Assert.Equal(single.Composite, scores[0].Composite, 9);
            Assert.NotEqual(scores[0].Composite, scores[1].Composite);
        }

        [Fact]
        public void NativeScoring_BatchInvalidArguments_ReturnMinusOneWithoutThrowing()
        {
            Assert.SkipUnless(NativeScoringBridge.IsAvailable, "native library not loaded");

            var inputs = new[] { ScoringProperty(), ScoringProperty() };
            var prefs = ScoringPrefs();
            var scores = SentinelBuffer(2);

            AssertStatus(-1, () => RawScorePropertyBatch(null!, 2, in prefs, scores, 2));
            AssertStatus(-1, () => RawScorePropertyBatch(inputs, 2, in prefs, null!, 2));
            AssertStatus(-1, () => RawScorePropertyBatch(inputs, 2, in Unsafe.NullRef<PreferencesInputNative>(), scores, 2));
            AssertStatus(-1, () => RawScorePropertyBatch(inputs, -1, in prefs, scores, 2));
            AssertStatus(-1, () => RawScorePropertyBatch(inputs, 2, in prefs, scores, -1));

            Assert.All(scores, s => Assert.Equal(Sentinel().Composite, s.Composite));
        }

        [Fact]
        public void NativeScoring_BatchOutputLengthBelowCount_ReturnsMinusThreeAndWritesNothing()
        {
            Assert.SkipUnless(NativeScoringBridge.IsAvailable, "native library not loaded");

            var inputs = new[] { ScoringProperty(), ScoringProperty(), ScoringProperty() };
            var prefs = ScoringPrefs();
            var scores = SentinelBuffer(4);

            // The array really has room for 4 records; the declared length is what the kernel must obey.
            AssertStatus(-3, () => RawScorePropertyBatch(inputs, 3, in prefs, scores, 2));
            AssertStatus(-3, () => RawScorePropertyBatch(inputs, 3, in prefs, scores, 0));

            Assert.All(scores, s => Assert.Equal(Sentinel().Composite, s.Composite));
        }

        [Fact]
        public void NativeScoring_BatchWritesExactlyCountRecords()
        {
            Assert.SkipUnless(NativeScoringBridge.IsAvailable, "native library not loaded");

            var inputs = new[] { ScoringProperty(), ScoringProperty() };
            var prefs = ScoringPrefs();
            var scores = SentinelBuffer(4);

            AssertStatus(0, () => RawScorePropertyBatch(inputs, 2, in prefs, scores, 4));

            Assert.NotEqual(Sentinel().Composite, scores[0].Composite);
            Assert.NotEqual(Sentinel().Composite, scores[1].Composite);
            Assert.Equal(Sentinel().Composite, scores[2].Composite);
            Assert.Equal(Sentinel().Composite, scores[3].Composite);
        }

        [Fact]
        public void NativeScoring_EmptyBatch_SucceedsEvenWithNullPointers()
        {
            Assert.SkipUnless(NativeScoringBridge.IsAvailable, "native library not loaded");

            AssertStatus(0, () => NativeScoringInterop.ScorePropertyBatch(null!, 0, in Unsafe.NullRef<PreferencesInputNative>(), null!, 0));
            AssertStatus(0, () => NativeScoringInterop.ScorePropertyBatch([], 0, in Unsafe.NullRef<PreferencesInputNative>(), [], 0));
        }

        [Fact]
        public void NativeScoring_SingleInvalidArguments_ReturnStatusWithoutThrowing()
        {
            Assert.SkipUnless(NativeScoringBridge.IsAvailable, "native library not loaded");

            var property = ScoringProperty();
            var prefs = ScoringPrefs();

            AssertStatus(-1, () => NativeScoringInterop.ScoreProperty(in Unsafe.NullRef<PropertyInputNative>(), in prefs, out _, 1));
            AssertStatus(-1, () => NativeScoringInterop.ScoreProperty(in property, in Unsafe.NullRef<PreferencesInputNative>(), out _, 1));
            AssertStatus(-1, () => NativeScoringInterop.ScoreProperty(in property, in prefs, out _, -1));
            AssertStatus(-3, () => NativeScoringInterop.ScoreProperty(in property, in prefs, out _, 0));
        }

        // A null `out` cannot be spelled through the managed declaration, which takes a reference, so
        // this reaches the entry point through a test-only declaration that takes the output as a pointer.
        [Fact]
        public void NativeScoring_SingleNullOutput_ReturnsMinusOne()
        {
            Assert.SkipUnless(NativeScoringBridge.IsAvailable, "native library not loaded");

            var property = ScoringProperty();
            var prefs = ScoringPrefs();

            AssertStatus(-1, () => RawScoreProperty(in property, in prefs, IntPtr.Zero, 1));
        }

        // Test-only declaration of the batch entry point with [In, Out] on the scores array, so a record the
        // kernel does not write keeps the value the test gave it. NativeScoringInterop declares the array
        // [Out] only, which hands back zeroed records for the ones the kernel leaves alone (observed), so
        // it cannot show whether the kernel wrote past `count` or declined to write; the tests that look
        // for that use this declaration.
        [DllImport("portfolio_scoring", EntryPoint = "ScorePropertyBatch", CallingConvention = CallingConvention.Cdecl)]
        private static extern int RawScorePropertyBatch([In] PropertyInputNative[] properties, int count, in PreferencesInputNative prefs, [In, Out] ScoreOutputNative[] scores, int outputLength);

        [DllImport("portfolio_scoring", EntryPoint = "ScoreProperty", CallingConvention = CallingConvention.Cdecl)]
        private static extern int RawScoreProperty(in PropertyInputNative property, in PreferencesInputNative prefs, IntPtr output, int outputLength);

        // Managed restatements of the two sub-scores that use int arithmetic. C# integer arithmetic is
        // unchecked by default and wraps; the kernel used raw int arithmetic, where overflow is undefined.
        private static double ManagedSizeScore(PropertyInputNative p, PreferencesInputNative prefs)
        {
            var ratio = prefs.MinSqft > 0 ? p.LotSqft / prefs.MinSqft : 1.0;
            var bonus = unchecked(p.Bedrooms - prefs.MinBedrooms) * 10.0;
            return Math.Clamp(Math.Min(ratio * 50, 100) + bonus, 0, 100);
        }

        private static double ManagedConditionScore(PropertyInputNative p, PreferencesInputNative prefs)
        {
            var average = (p.RoofCondition + p.AcCondition + p.PlumbingCondition + p.ElectricalCondition) / 4.0;
            var recencyBonus = p.LastRenovationYear > 0
                ? unchecked(Math.Max(0, 10 - (prefs.CurrentYear - p.LastRenovationYear))) * 2.0
                : 0.0;
            return Math.Clamp(average + recencyBonus, 0, 100);
        }

        public static TheoryData<int, int> BedroomExtremes => new()
        {
            { int.MaxValue, -1 },
            { int.MinValue, 1 },
            { int.MinValue, int.MaxValue },
            { int.MaxValue, int.MinValue },
            { 3, 3 },
            { 0, 100 }
        };

        [Theory]
        [MemberData(nameof(BedroomExtremes))]
        public void NativeScoring_BedroomDifferenceOverflow_WrapsLikeUncheckedCSharp(int bedrooms, int minBedrooms)
        {
            Assert.SkipUnless(NativeScoringBridge.IsAvailable, "native library not loaded");

            var property = ScoringProperty() with { Bedrooms = bedrooms };
            var prefs = ScoringPrefs() with { MinBedrooms = minBedrooms };
            var scores = new ScoreOutputNative[1];

            AssertStatus(0, () => NativeScoringInterop.ScorePropertyBatch([property], 1, in prefs, scores, 1));

            Assert.Equal(ManagedSizeScore(property, prefs), scores[0].Size, 9);
        }

        public static TheoryData<int, int> RenovationYearExtremes => new()
        {
            { int.MinValue, int.MaxValue },
            { int.MaxValue, 1 },
            { int.MinValue, 1 },
            { 0, int.MaxValue },
            { 2026, 2021 },
            { 2026, 1 }
        };

        [Theory]
        [MemberData(nameof(RenovationYearExtremes))]
        public void NativeScoring_RenovationYearOverflow_WrapsLikeUncheckedCSharp(int currentYear, int lastRenovationYear)
        {
            Assert.SkipUnless(NativeScoringBridge.IsAvailable, "native library not loaded");

            var property = ScoringProperty() with { LastRenovationYear = lastRenovationYear };
            var prefs = ScoringPrefs() with { CurrentYear = currentYear };
            var scores = new ScoreOutputNative[1];

            AssertStatus(0, () => NativeScoringInterop.ScorePropertyBatch([property], 1, in prefs, scores, 1));

            Assert.Equal(ManagedConditionScore(property, prefs), scores[0].Condition, 9);
        }

        [Fact]
        public void ScoringBridge_EmptyBatch_ReturnsEmptyArray()
        {
            Assert.SkipUnless(NativeScoringBridge.IsAvailable, "native library not loaded");

            var outputs = NativeScoringBridge.ScorePropertyBatch(new List<Property>(), new HomeSearchPreferencesDto());

            Assert.Empty(outputs);
        }

        [Theory]
        [InlineData(-1, "invalid argument")]
        [InlineData(-3, "output buffer too small")]
        [InlineData(-99, "native exception")]
        [InlineData(7, "unknown")]
        public void ScoringBridge_NonZeroStatus_ThrowsAClassifiedNativeFailure(int status, string reason)
        {
            var thrown = Assert.Throws<InvalidOperationException>(() => NativeScoringBridge.ThrowIfFailed(status));

            Assert.Contains($"status {status} ({reason})", thrown.Message);
            Assert.True(NativeKernelLoader.IsNativeInvocationException(thrown));
        }

        [Fact]
        public void ScoringBridge_ZeroStatus_DoesNotThrow()
        {
            NativeScoringBridge.ThrowIfFailed(0);
        }

        // ---------------------------------------------------------------------------------------
        // Status mapping of the other two bridges. These run without the native libraries; the
        // Try... wrappers around them are covered through real kernel failures above.
        // ---------------------------------------------------------------------------------------

        [Theory]
        [InlineData(-1, "invalid argument")]
        [InlineData(-2, "aggregate buffer too small")]
        [InlineData(-99, "native exception")]
        [InlineData(5, "unknown")]
        public void GeoStreamBridge_NonZeroStatus_ThrowsAClassifiedNativeFailure(int status, string reason)
        {
            var thrown = Assert.Throws<InvalidOperationException>(() => GeoStreamNativeBridge.ThrowIfFailed(status));

            Assert.Contains($"status {status} ({reason})", thrown.Message);
            Assert.True(NativeKernelLoader.IsNativeInvocationException(thrown));
        }

        [Fact]
        public void GeoStreamBridge_ZeroStatus_DoesNotThrow()
        {
            GeoStreamNativeBridge.ThrowIfFailed(0);
        }

        [Theory]
        [InlineData(-1, "invalid argument")]
        [InlineData(-99, "native exception")]
        [InlineData(-7, "unknown")]
        public void ClusterBridge_NegativeStatus_ThrowsAClassifiedNativeFailure(int status, string reason)
        {
            var thrown = Assert.Throws<InvalidOperationException>(() => SpatialClusterNativeBridge.ThrowIfFailed(status));

            Assert.Contains($"status {status} ({reason})", thrown.Message);
            Assert.True(NativeKernelLoader.IsNativeInvocationException(thrown));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        public void ClusterBridge_NonNegativeStatus_IsAClusterCountNotAFailure(int clusterCount)
        {
            SpatialClusterNativeBridge.ThrowIfFailed(clusterCount);
        }
    }
}
