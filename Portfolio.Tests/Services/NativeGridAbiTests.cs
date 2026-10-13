using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Native;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    // ABI and parity tests for the two grid kernels (viewshed_kernel, raster_terrain_kernel).
    //
    // Three kinds of test live here:
    //  * Service-level tests that run on every path (native present, native disabled, or no native
    //    library at all) and compare the service result with an independent managed reference. They
    //    pin the "native and managed are result-identical" contract for non-square grids, which
    //    catches a width/height mix-up that a square-grid fixture cannot, and show that managed
    //    validation turns away hostile dimensions and lengths before any bridge is reached.
    //  * Native-gated bridge tests that drive the bridges directly and compare with the reference.
    //  * Native-gated ABI tests that call the C entry points through the interop declarations and
    //    check the documented status codes: the elevation length must equal width * height, and
    //    dimensions are checked in 64 bits so a product that wraps a 32-bit int is refused.
    // The native-gated tests are reported as Skipped when the library is absent (the normal
    // clean-clone state), like NativeParityTests.
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

        // ---- Native-gated ABI: the C entry points called directly (see the headers for the contract) ----
        //
        // The calls below hand the kernels lengths and dimensions that a correct kernel must refuse
        // BEFORE it touches either buffer. Every array is really as long as the length it is declared
        // with (the kernel cannot detect a caller that lies about a length), so a kernel that
        // wrongly accepted one of these inputs would read or write out of bounds rather than return.

        private const int InvalidArgument = -1;
        private const int ObserverOutsideGrid = -2;
        private const int DegenerateExtent = -2;
        private const byte Sentinel = 0xEE;

        // Dimensions whose 32-bit product wraps to zero, a small positive number or a negative one.
        public static TheoryData<int, int> WrappingDimensions => new()
        {
            { 65536, 65536 },            // 2^32      -> 0
            { 65537, 65537 },            // 2^32 + .. -> 131073
            { 46341, 46341 },            // just over int.MaxValue -> negative
            { 3, 1_431_655_766 },        // 2^32 + 2  -> 2
            { int.MaxValue, 2 },         // -> -2
            { 2, int.MaxValue },
            { int.MaxValue, int.MaxValue }, // -> 1
        };

        // Dimensions that are not positive, including a pair whose product is positive.
        public static TheoryData<int, int> NonPositiveDimensions => new()
        {
            { 0, 4 },
            { 4, 0 },
            { -1, 4 },
            { 4, -1 },
            { -4, -4 },
            { int.MinValue, int.MinValue },
        };

        // Declared element counts that disagree with a 4 x 4 grid.
        public static TheoryData<int> MismatchedLengths => new() { -1, 0, 1, 15, 17, 32 };

        private static int ViewshedCall(
            double[]? elevation, int elevationLength, int width, int height,
            byte[]? visibility, int outputLength,
            double cellSize = 30, int observerX = 0, int observerY = 0)
            => ViewshedNativeInterop.Compute(
                elevation!, elevationLength, width, height, cellSize, observerX, observerY, 2.0, visibility!, outputLength);

        private static int HillshadeCall(
            double[]? elevation, int elevationLength, int width, int height,
            byte[]? intensities, int outputLength, double cellSize = 30)
            => RasterTerrainNativeInterop.GenerateHillshade(
                elevation!, elevationLength, width, height, cellSize, 315, 45, intensities!, outputLength);

        private static int HeatmapCall(
            WeightedPointNative[]? points, int pointCount, RasterExtentNative extent, int width, int height,
            double[]? values, int outputLength, double radius = 0.7)
            => RasterTerrainNativeInterop.GenerateHeatmap(
                points!, pointCount, in extent, width, height, radius, values!, outputLength);

        private static byte[] Filled(int length) => Enumerable.Repeat(Sentinel, Math.Max(length, 1)).ToArray();

        private static readonly RasterExtentNative UnitExtent = new() { MinX = 0, MinY = 0, MaxX = 1, MaxY = 1 };

        private static WeightedPointNative[] OnePoint() => [new WeightedPointNative { X = 0.5, Y = 0.5, Weight = 1 }];

        [Fact]
        public void NativeViewshed_Abi_ValidInput_ReturnsVisibleCountAndMatchesReference()
        {
            Assert.SkipUnless(ViewshedNativeBridge.IsAvailable, SkipReason);
            var request = ViewshedRequest(37, 23, observerX: 5, observerY: 17);
            var elevation = request.Elevation.ToArray();
            var visibility = new byte[37 * 23];

            var status = ViewshedCall(elevation, elevation.Length, 37, 23, visibility, visibility.Length, observerX: 5, observerY: 17);

            var expected = ReferenceViewshed(request);
            Assert.Equal(expected.Count(v => v == 1), status);
            Assert.Equal(expected.ToArray(), visibility);
        }

        [Fact]
        public void NativeViewshed_Abi_OutputBufferLargerThanTheGrid_WritesOnlyTheGrid()
        {
            Assert.SkipUnless(ViewshedNativeBridge.IsAvailable, SkipReason);
            var request = ViewshedRequest(9, 5, observerX: 4, observerY: 2);
            var elevation = request.Elevation.ToArray();
            var visibility = Filled(45 + 16);

            var status = ViewshedCall(elevation, elevation.Length, 9, 5, visibility, visibility.Length, observerX: 4, observerY: 2);

            Assert.True(status > 0);
            Assert.Equal(ReferenceViewshed(request).ToArray(), visibility[..45]);
            Assert.All(visibility[45..], b => Assert.Equal(Sentinel, b));
        }

        [Theory]
        [MemberData(nameof(MismatchedLengths))]
        public void NativeViewshed_Abi_ElevationLengthThatIsNotWidthTimesHeight_IsRejected(int elevationLength)
        {
            Assert.SkipUnless(ViewshedNativeBridge.IsAvailable, SkipReason);
            var elevation = new double[Math.Max(elevationLength, 1)];
            var visibility = Filled(16);

            var status = ViewshedCall(elevation, elevationLength, 4, 4, visibility, visibility.Length);

            Assert.Equal(InvalidArgument, status);
            Assert.All(visibility, b => Assert.Equal(Sentinel, b));
        }

        [Fact]
        public void NativeViewshed_Abi_OutputBufferSmallerThanTheGrid_IsRejected()
        {
            Assert.SkipUnless(ViewshedNativeBridge.IsAvailable, SkipReason);
            var elevation = new double[16];
            var visibility = Filled(15);

            var status = ViewshedCall(elevation, 16, 4, 4, visibility, 15);

            Assert.Equal(InvalidArgument, status);
            Assert.All(visibility, b => Assert.Equal(Sentinel, b));
        }

        [Theory]
        [MemberData(nameof(WrappingDimensions))]
        public void NativeViewshed_Abi_DimensionsWhoseProductOverflowsInt_AreRejected(int width, int height)
        {
            Assert.SkipUnless(ViewshedNativeBridge.IsAvailable, SkipReason);
            // Declare the lengths a 32-bit multiply would have produced, which is what an unguarded
            // `outputLength < width * height` check is fooled by.
            var wrapped = unchecked(width * height);
            var elevation = new double[Math.Max(wrapped, 1)];
            var visibility = Filled(wrapped);

            var status = ViewshedCall(elevation, wrapped, width, height, visibility, wrapped);

            Assert.Equal(InvalidArgument, status);
            Assert.All(visibility, b => Assert.Equal(Sentinel, b));
        }

        [Theory]
        [MemberData(nameof(NonPositiveDimensions))]
        public void NativeViewshed_Abi_NonPositiveDimensions_AreRejected(int width, int height)
        {
            Assert.SkipUnless(ViewshedNativeBridge.IsAvailable, SkipReason);
            var elevation = new double[16];
            var visibility = Filled(16);

            var status = ViewshedCall(elevation, 16, width, height, visibility, 16);

            Assert.Equal(InvalidArgument, status);
            Assert.All(visibility, b => Assert.Equal(Sentinel, b));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-30.0)]
        public void NativeViewshed_Abi_NonPositiveCellSize_IsRejected(double cellSize)
        {
            Assert.SkipUnless(ViewshedNativeBridge.IsAvailable, SkipReason);

            Assert.Equal(InvalidArgument, ViewshedCall(new double[16], 16, 4, 4, Filled(16), 16, cellSize: cellSize));
        }

        [Fact]
        public void NativeViewshed_Abi_NullBuffers_AreRejected()
        {
            Assert.SkipUnless(ViewshedNativeBridge.IsAvailable, SkipReason);

            Assert.Equal(InvalidArgument, ViewshedCall(null, 16, 4, 4, Filled(16), 16));
            Assert.Equal(InvalidArgument, ViewshedCall(new double[16], 16, 4, 4, null, 16));
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(4, 0)]
        [InlineData(0, -1)]
        [InlineData(0, 4)]
        public void NativeViewshed_Abi_ObserverOutsideTheGrid_ReportsItsOwnStatus(int observerX, int observerY)
        {
            Assert.SkipUnless(ViewshedNativeBridge.IsAvailable, SkipReason);
            var visibility = Filled(16);

            var status = ViewshedCall(new double[16], 16, 4, 4, visibility, 16, observerX: observerX, observerY: observerY);

            Assert.Equal(ObserverOutsideGrid, status);
            Assert.All(visibility, b => Assert.Equal(Sentinel, b));
        }

        [Fact]
        public void NativeViewshed_Bridge_RequestWhoseElevationCountDisagreesWithTheGrid_IsDeclined()
        {
            Assert.SkipUnless(ViewshedNativeBridge.IsAvailable, SkipReason);
            var request = ViewshedRequest(4, 4, observerX: 0, observerY: 0);
            request.Elevation.RemoveAt(request.Elevation.Count - 1);   // 15 values for a 4 x 4 grid

            // The bridge turns the kernel's status into an exception, TryCompute logs it and reports
            // "not handled" so the caller falls back to managed code. Nothing is read past the array.
            Assert.False(ViewshedNativeBridge.TryCompute(request, NullLogger.Instance, out var result));
            Assert.Null(result);
        }

        [Fact]
        public void NativeHillshade_Abi_ValidInput_MatchesReference()
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);
            var request = HillshadeRequest(41, 29);
            var elevation = request.Elevation.ToArray();
            var intensities = new byte[41 * 29];

            var status = HillshadeCall(elevation, elevation.Length, 41, 29, intensities, intensities.Length);

            Assert.Equal(0, status);
            Assert.Equal(ReferenceHillshade(request).ToArray(), intensities);
        }

        [Fact]
        public void NativeHillshade_Abi_OutputBufferLargerThanTheGrid_WritesOnlyTheGrid()
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);
            var request = HillshadeRequest(9, 5);
            var elevation = request.Elevation.ToArray();
            var intensities = Filled(45 + 16);

            var status = HillshadeCall(elevation, elevation.Length, 9, 5, intensities, intensities.Length);

            Assert.Equal(0, status);
            Assert.Equal(ReferenceHillshade(request).ToArray(), intensities[..45]);
            Assert.All(intensities[45..], b => Assert.Equal(Sentinel, b));
        }

        [Theory]
        [MemberData(nameof(MismatchedLengths))]
        public void NativeHillshade_Abi_ElevationLengthThatIsNotWidthTimesHeight_IsRejected(int elevationLength)
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);
            var elevation = new double[Math.Max(elevationLength, 1)];
            var intensities = Filled(16);

            var status = HillshadeCall(elevation, elevationLength, 4, 4, intensities, intensities.Length);

            Assert.Equal(InvalidArgument, status);
            Assert.All(intensities, b => Assert.Equal(Sentinel, b));
        }

        [Fact]
        public void NativeHillshade_Abi_OutputBufferSmallerThanTheGrid_IsRejected()
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);
            var intensities = Filled(15);

            var status = HillshadeCall(new double[16], 16, 4, 4, intensities, 15);

            Assert.Equal(InvalidArgument, status);
            Assert.All(intensities, b => Assert.Equal(Sentinel, b));
        }

        [Theory]
        [MemberData(nameof(WrappingDimensions))]
        public void NativeHillshade_Abi_DimensionsWhoseProductOverflowsInt_AreRejected(int width, int height)
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);
            var wrapped = unchecked(width * height);
            var elevation = new double[Math.Max(wrapped, 1)];
            var intensities = Filled(wrapped);

            var status = HillshadeCall(elevation, wrapped, width, height, intensities, wrapped);

            Assert.Equal(InvalidArgument, status);
            Assert.All(intensities, b => Assert.Equal(Sentinel, b));
        }

        [Theory]
        [MemberData(nameof(NonPositiveDimensions))]
        public void NativeHillshade_Abi_NonPositiveDimensions_AreRejected(int width, int height)
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);
            var intensities = Filled(16);

            var status = HillshadeCall(new double[16], 16, width, height, intensities, 16);

            Assert.Equal(InvalidArgument, status);
            Assert.All(intensities, b => Assert.Equal(Sentinel, b));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-30.0)]
        public void NativeHillshade_Abi_NonPositiveCellSize_IsRejected(double cellSize)
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);

            Assert.Equal(InvalidArgument, HillshadeCall(new double[16], 16, 4, 4, Filled(16), 16, cellSize));
        }

        [Fact]
        public void NativeHillshade_Abi_NullBuffers_AreRejected()
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);

            Assert.Equal(InvalidArgument, HillshadeCall(null, 16, 4, 4, Filled(16), 16));
            Assert.Equal(InvalidArgument, HillshadeCall(new double[16], 16, 4, 4, null, 16));
        }

        [Fact]
        public void NativeHillshade_Bridge_RequestWhoseElevationCountDisagreesWithTheGrid_Throws()
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);
            var request = HillshadeRequest(4, 4);
            request.Elevation.RemoveAt(request.Elevation.Count - 1);   // 15 values for a 4 x 4 grid

            var ex = Assert.Throws<InvalidOperationException>(() => RasterTerrainNativeBridge.GenerateHillshade(request));
            Assert.Contains("-1", ex.Message);
        }

        [Fact]
        public void NativeHeatmap_Abi_ValidInput_MatchesReference()
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);
            var request = HeatmapRequest(33, 21);
            var points = request.Points.Select(p => new WeightedPointNative { X = p.X, Y = p.Y, Weight = p.Weight }).ToArray();
            var extent = new RasterExtentNative { MinX = request.MinX, MinY = request.MinY, MaxX = request.MaxX, MaxY = request.MaxY };
            var values = new double[33 * 21];

            var status = HeatmapCall(points, points.Length, extent, 33, 21, values, values.Length, request.Radius);

            Assert.Equal(0, status);
            var expected = ReferenceHeatmap(request);
            for (var i = 0; i < expected.Count; i++)
                Assert.Equal(expected[i], values[i], 9);
        }

        [Fact]
        public void NativeHeatmap_Abi_OutputBufferLargerThanTheGrid_WritesOnlyTheGrid()
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);
            var values = Enumerable.Repeat(-7.0, 12 + 5).ToArray();

            var status = HeatmapCall(OnePoint(), 1, UnitExtent, 4, 3, values, values.Length);

            Assert.Equal(0, status);
            Assert.All(values[..12], v => Assert.InRange(v, 0.0, 1.0));
            Assert.All(values[12..], v => Assert.Equal(-7.0, v));
        }

        [Fact]
        public void NativeHeatmap_Abi_OutputBufferSmallerThanTheGrid_IsRejected()
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);
            var values = Enumerable.Repeat(-7.0, 11).ToArray();

            var status = HeatmapCall(OnePoint(), 1, UnitExtent, 4, 3, values, 11);

            Assert.Equal(InvalidArgument, status);
            Assert.All(values, v => Assert.Equal(-7.0, v));
        }

        [Theory]
        [MemberData(nameof(WrappingDimensions))]
        public void NativeHeatmap_Abi_DimensionsWhoseProductOverflowsInt_AreRejected(int width, int height)
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);
            var wrapped = unchecked(width * height);
            var values = Enumerable.Repeat(-7.0, Math.Max(wrapped, 1)).ToArray();

            var status = HeatmapCall(OnePoint(), 1, UnitExtent, width, height, values, wrapped);

            Assert.Equal(InvalidArgument, status);
            Assert.All(values, v => Assert.Equal(-7.0, v));
        }

        [Theory]
        [MemberData(nameof(NonPositiveDimensions))]
        public void NativeHeatmap_Abi_NonPositiveDimensions_AreRejected(int width, int height)
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);
            var values = Enumerable.Repeat(-7.0, 16).ToArray();

            var status = HeatmapCall(OnePoint(), 1, UnitExtent, width, height, values, 16);

            Assert.Equal(InvalidArgument, status);
            Assert.All(values, v => Assert.Equal(-7.0, v));
        }

        [Fact]
        public void NativeHeatmap_Abi_InvalidScalarsAndPointers_AreRejectedWithTheirOwnStatus()
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);
            var values = new double[12];
            var inverted = new RasterExtentNative { MinX = 1, MinY = 0, MaxX = 0, MaxY = 1 };

            Assert.Equal(InvalidArgument, HeatmapCall(OnePoint(), -1, UnitExtent, 4, 3, values, 12));
            Assert.Equal(InvalidArgument, HeatmapCall(OnePoint(), 1, UnitExtent, 4, 3, values, 12, radius: 0));
            Assert.Equal(InvalidArgument, HeatmapCall(null, 1, UnitExtent, 4, 3, values, 12));
            Assert.Equal(InvalidArgument, HeatmapCall(OnePoint(), 1, UnitExtent, 4, 3, null, 12));
            Assert.Equal(DegenerateExtent, HeatmapCall(OnePoint(), 1, inverted, 4, 3, values, 12));
        }

        [Fact]
        public void NativeHeatmap_Abi_ZeroPoints_ProducesAnAllZeroGrid()
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, SkipReason);
            var values = Enumerable.Repeat(-7.0, 12).ToArray();

            var status = HeatmapCall([], 0, UnitExtent, 4, 3, values, 12);

            Assert.Equal(0, status);
            Assert.All(values, v => Assert.Equal(0.0, v));
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
