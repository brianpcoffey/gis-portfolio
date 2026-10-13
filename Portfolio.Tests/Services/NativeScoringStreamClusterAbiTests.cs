using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
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
    }
}
