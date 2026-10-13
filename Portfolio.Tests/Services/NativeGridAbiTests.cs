using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Native;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    // ABI and parity tests for the two grid kernels (viewshed_kernel, raster_terrain_kernel).
    //
    // Two kinds of test live here:
    //  * Service-level tests that run on every path (native present, native disabled, or no native
    //    library at all) and compare the service result with an independent managed reference. They
    //    pin the "native and managed are result-identical" contract for non-square grids, which
    //    catches a width/height mix-up that a square-grid fixture cannot.
    //  * Native-gated tests that drive the kernels directly and are reported as Skipped when the
    //    library is absent (the normal clean-clone state), like NativeParityTests.
    //
    // The managed references below are deliberately duplicated rather than reused from the services:
    // they encode the contract the native kernels must honour, so they must not drift if a service
    // is refactored.
    public class NativeGridAbiTests
    {
        private const string SkipReason = "native library not loaded";

        // ---- Valid input: service result (native or managed) equals the independent reference ----

        [Fact]
        public async Task Viewshed_Service_MatchesReference_OnANonSquareGrid()
        {
            var request = ViewshedRequest(37, 23, observerX: 5, observerY: 17);

            var result = await new ViewshedService(NullLogger<ViewshedService>.Instance).ComputeAsync(request);

            var expected = ReferenceViewshed(request);
            Assert.Equal(37, result.Width);
            Assert.Equal(23, result.Height);
            Assert.Equal(37 * 23, result.TotalCells);
            Assert.Equal(expected.Count(v => v == 1), result.VisibleCells);
            Assert.Equal(expected, result.Visibility);
        }

        [Fact]
        public async Task Hillshade_Service_MatchesReference_OnANonSquareGrid()
        {
            var request = HillshadeRequest(41, 29);

            var result = await new RasterTerrainService(NullLogger<RasterTerrainService>.Instance).GenerateHillshadeAsync(request);

            Assert.Equal(41, result.Width);
            Assert.Equal(29, result.Height);
            Assert.Equal(ReferenceHillshade(request), result.Intensities);
        }

        [Fact]
        public async Task Heatmap_Service_MatchesReference_OnANonSquareGrid()
        {
            var request = HeatmapRequest(33, 21);

            var result = await new RasterTerrainService(NullLogger<RasterTerrainService>.Instance).GenerateHeatmapAsync(request);

            Assert.Equal(33, result.Width);
            Assert.Equal(21, result.Height);
            var expected = ReferenceHeatmap(request);
            Assert.Equal(expected.Count, result.Values.Count);
            for (var i = 0; i < expected.Count; i++)
                Assert.Equal(Math.Round(expected[i], 4), result.Values[i]);
        }

        [Fact]
        public async Task Heatmap_Service_WithNoPoints_ReturnsAnAllZeroGrid()
        {
            var request = HeatmapRequest(6, 4);
            request.Points = [];

            var result = await new RasterTerrainService(NullLogger<RasterTerrainService>.Instance).GenerateHeatmapAsync(request);

            Assert.Equal(24, result.Values.Count);
            Assert.All(result.Values, v => Assert.Equal(0.0, v));
        }

        // ---- Managed validation runs before any bridge call, so the native guards are defence in depth ----

        public static TheoryData<int, int> OverflowingDimensions => new()
        {
            { 65536, 65536 },          // product is 2^32: wraps to 0 in 32-bit arithmetic
            { 46341, 46341 },          // product just above int.MaxValue: wraps negative
            { int.MaxValue, 2 },       // wraps to -2
            { 2, int.MaxValue },
            { int.MaxValue, int.MaxValue },
            { 0, 8 },
            { 8, 0 },
            { -65536, 65536 },
            { 65536, -65536 },
            { int.MinValue, int.MinValue },
        };

        [Theory]
        [MemberData(nameof(OverflowingDimensions))]
        public async Task Viewshed_Service_RejectsOverflowingOrNonPositiveDimensions(int width, int height)
        {
            var request = ViewshedRequest(4, 4, observerX: 0, observerY: 0);
            request.Width = width;
            request.Height = height;

            await Assert.ThrowsAsync<ArgumentException>(() =>
                new ViewshedService(NullLogger<ViewshedService>.Instance).ComputeAsync(request));
        }

        [Theory]
        [MemberData(nameof(OverflowingDimensions))]
        public async Task Hillshade_Service_RejectsOverflowingOrNonPositiveDimensions(int width, int height)
        {
            var request = HillshadeRequest(4, 4);
            request.Width = width;
            request.Height = height;

            await Assert.ThrowsAsync<ArgumentException>(() =>
                new RasterTerrainService(NullLogger<RasterTerrainService>.Instance).GenerateHillshadeAsync(request));
        }

        [Theory]
        [MemberData(nameof(OverflowingDimensions))]
        public async Task Heatmap_Service_RejectsOverflowingOrNonPositiveDimensions(int width, int height)
        {
            var request = HeatmapRequest(4, 4);
            request.Width = width;
            request.Height = height;

            await Assert.ThrowsAsync<ArgumentException>(() =>
                new RasterTerrainService(NullLogger<RasterTerrainService>.Instance).GenerateHeatmapAsync(request));
        }

        [Theory]
        [InlineData(15)]
        [InlineData(17)]
        [InlineData(0)]
        public async Task Viewshed_Service_RejectsAnElevationCountThatIsNotWidthTimesHeight(int count)
        {
            var request = ViewshedRequest(4, 4, observerX: 0, observerY: 0);
            request.Elevation = Enumerable.Repeat(0.0, count).ToList();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                new ViewshedService(NullLogger<ViewshedService>.Instance).ComputeAsync(request));
        }

        [Theory]
        [InlineData(15)]
        [InlineData(17)]
        [InlineData(0)]
        public async Task Hillshade_Service_RejectsAnElevationCountThatIsNotWidthTimesHeight(int count)
        {
            var request = HillshadeRequest(4, 4);
            request.Elevation = Enumerable.Repeat(0.0, count).ToList();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                new RasterTerrainService(NullLogger<RasterTerrainService>.Instance).GenerateHillshadeAsync(request));
        }

        // ---- Native-gated: the bridges drive the kernels directly and must agree with the reference ----

        [Fact]
        public void NativeViewshed_Bridge_MatchesReference_OnANonSquareGrid()
        {
            Assert.SkipUnless(ViewshedNativeBridge.IsAvailable, SkipReason);
            var request = ViewshedRequest(37, 23, observerX: 5, observerY: 17);

            Assert.True(ViewshedNativeBridge.TryCompute(request, NullLogger.Instance, out var native));

            var expected = ReferenceViewshed(request);
            Assert.True(native!.NativeAccelerated);
            Assert.Equal(expected.Count(v => v == 1), native.VisibleCells);
            Assert.Equal(expected, native.Visibility);
        }

        [Fact]
        public void NativeHillshade_Bridge_MatchesReference_OnANonSquareGrid()
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);
            var request = HillshadeRequest(41, 29);

            var native = RasterTerrainNativeBridge.GenerateHillshade(request);

            Assert.True(native.NativeAccelerated);
            Assert.Equal(ReferenceHillshade(request), native.Intensities);
        }

        [Fact]
        public void NativeHeatmap_Bridge_MatchesReference_OnANonSquareGrid()
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);
            var request = HeatmapRequest(33, 21);

            var native = RasterTerrainNativeBridge.GenerateHeatmap(request);

            Assert.True(native.NativeAccelerated);
            var expected = ReferenceHeatmap(request);
            Assert.Equal(expected.Count, native.Values.Count);
            for (var i = 0; i < expected.Count; i++)
                Assert.Equal(expected[i], native.Values[i], 9);
        }

        // ---- Fixtures ----

        private static List<double> Terrain(int width, int height)
        {
            var cells = new List<double>(width * height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var dx = x - width / 3.0;
                    var dy = y - height / 2.0;
                    cells.Add(40.0 * Math.Sin(x / 5.0) * Math.Cos(y / 4.0)
                        + 25.0 * Math.Exp(-(dx * dx + dy * dy) / 60.0)
                        + (x * 7 + y * 13) % 11);
                }
            }

            return cells;
        }

        private static ViewshedRequestDto ViewshedRequest(int width, int height, int observerX, int observerY) => new()
        {
            Width = width,
            Height = height,
            CellSize = 30,
            ObserverX = observerX,
            ObserverY = observerY,
            ObserverHeight = 2,
            Elevation = Terrain(width, height)
        };

        private static RasterHillshadeRequestDto HillshadeRequest(int width, int height) => new()
        {
            Width = width,
            Height = height,
            CellSize = 30,
            AzimuthDegrees = 315,
            AltitudeDegrees = 45,
            Elevation = Terrain(width, height)
        };

        private static HeatmapRequestDto HeatmapRequest(int width, int height) => new()
        {
            Width = width,
            Height = height,
            MinX = -2,
            MinY = 1,
            MaxX = 4,
            MaxY = 3,
            Radius = 0.7,
            Points =
            [
                new WeightedPointDto { X = 0.0, Y = 1.5, Weight = 1.0 },
                new WeightedPointDto { X = 1.25, Y = 2.0, Weight = 2.5 },
                new WeightedPointDto { X = 3.0, Y = 2.75, Weight = 0.5 },
                new WeightedPointDto { X = -1.0, Y = 1.1, Weight = 1.5 },
                new WeightedPointDto { X = 2.2, Y = 1.6, Weight = 3.0 }
            ]
        };

        // ---- Independent managed references (see the class comment) ----

        private static List<byte> ReferenceViewshed(ViewshedRequestDto request)
        {
            var width = request.Width;
            var height = request.Height;
            var elevation = request.Elevation;
            var visibility = new byte[width * height];
            var observerElevation = elevation[request.ObserverY * width + request.ObserverX] + request.ObserverHeight;

            for (var ty = 0; ty < height; ty++)
            {
                for (var tx = 0; tx < width; tx++)
                {
                    if (tx == request.ObserverX && ty == request.ObserverY)
                    {
                        visibility[ty * width + tx] = 1;
                        continue;
                    }

                    var dx = (double)(tx - request.ObserverX);
                    var dy = (double)(ty - request.ObserverY);
                    var steps = (int)Math.Max(Math.Abs(dx), Math.Abs(dy));
                    var targetDistance = Math.Sqrt(dx * dx + dy * dy) * request.CellSize;
                    var targetAngle = (elevation[ty * width + tx] - observerElevation) / targetDistance;

                    var maxAngle = double.NegativeInfinity;
                    var blocked = false;
                    for (var i = 1; i < steps; i++)
                    {
                        var fraction = (double)i / steps;
                        var cx = (int)Math.Floor(request.ObserverX + dx * fraction + 0.5);
                        var cy = (int)Math.Floor(request.ObserverY + dy * fraction + 0.5);
                        var distance = fraction * targetDistance;
                        if (distance <= 0)
                            continue;

                        var angle = (elevation[cy * width + cx] - observerElevation) / distance;
                        if (angle > maxAngle)
                            maxAngle = angle;
                        if (targetAngle < maxAngle)
                        {
                            blocked = true;
                            break;
                        }
                    }

                    visibility[ty * width + tx] = blocked ? (byte)0 : (byte)1;
                }
            }

            return [.. visibility];
        }

        private static List<byte> ReferenceHillshade(RasterHillshadeRequestDto request)
        {
            var width = request.Width;
            var height = request.Height;
            var elevation = request.Elevation;
            var output = new byte[width * height];
            var azimuth = (360.0 - request.AzimuthDegrees + 90.0) * Math.PI / 180.0;
            var altitude = request.AltitudeDegrees * Math.PI / 180.0;

            double Sample(int x, int y) => elevation[Math.Clamp(y, 0, height - 1) * width + Math.Clamp(x, 0, width - 1)];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var dzdx = Sample(x + 1, y) - Sample(x - 1, y);
                    var dzdy = Sample(x, y + 1) - Sample(x, y - 1);
                    var slope = Math.Atan(Math.Sqrt(dzdx * dzdx + dzdy * dzdy) / (2.0 * request.CellSize));
                    var aspect = Math.Atan2(dzdy, -dzdx);
                    var shaded = Math.Sin(altitude) * Math.Cos(slope)
                        + Math.Cos(altitude) * Math.Sin(slope) * Math.Cos(azimuth - aspect);
                    output[y * width + x] = (byte)Math.Clamp(Math.Round(255.0 * Math.Max(0, shaded)), 0, 255);
                }
            }

            return [.. output];
        }

        private static List<double> ReferenceHeatmap(HeatmapRequestDto request)
        {
            var width = request.Width;
            var height = request.Height;
            var values = new double[width * height];
            var xStep = (request.MaxX - request.MinX) / width;
            var yStep = (request.MaxY - request.MinY) / height;
            var max = 0.0;

            for (var y = 0; y < height; y++)
            {
                var py = request.MinY + (y + 0.5) * yStep;
                for (var x = 0; x < width; x++)
                {
                    var px = request.MinX + (x + 0.5) * xStep;
                    var value = 0.0;
                    foreach (var point in request.Points)
                    {
                        var dx = px - point.X;
                        var dy = py - point.Y;
                        value += point.Weight * Math.Exp(-(dx * dx + dy * dy) / (2.0 * request.Radius * request.Radius));
                    }

                    values[y * width + x] = value;
                    max = Math.Max(max, value);
                }
            }

            if (max > 0)
            {
                for (var i = 0; i < values.Length; i++)
                    values[i] /= max;
            }

            return [.. values];
        }
    }
}
