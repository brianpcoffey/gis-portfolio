using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Portfolio.Services.Native;

namespace Portfolio.Tests.Services
{
    public class NativeKernelLoaderTests
    {
        [Fact]
        public void TryLoad_UnknownKernel_ReturnsFalse()
        {
            Assert.False(NativeKernelLoader.TryLoad("no_such_kernel"));
        }

        public static TheoryData<Exception, bool> Classifications => new()
        {
            { new DllNotFoundException(), true },
            { new EntryPointNotFoundException(), true },
            { new BadImageFormatException(), true },
            { new SEHException(), true },
            { new InvalidOperationException(), true },
            { new ArgumentException(), false },
            { new NullReferenceException(), false },
            { new OutOfMemoryException(), false },
            { new Exception(), false },
        };

        [Theory]
        [MemberData(nameof(Classifications))]
        public void IsNativeInvocationException_TruthTable(Exception exception, bool expected)
        {
            Assert.Equal(expected, NativeKernelLoader.IsNativeInvocationException(exception));
        }

        // ---------------------------------------------------------------------------------------
        // ABI version guard. A kernel whose exported signatures changed keeps its export names, so a
        // library built before the change still loads; only the version export tells it apart. The
        // bridges of those kernels pass their P/Invoke declaration of it to NativeKernelLoader.Load,
        // and these tests drive the decision with stand-ins, so they run without any native library.
        // ---------------------------------------------------------------------------------------

        [Fact]
        public void CheckAbiVersion_ProbeReturnsTheExpectedVersion_IsLoaded()
        {
            Assert.Equal(NativeLoadResult.Loaded, NativeKernelLoader.CheckAbiVersion(() => 2, 2));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(-1)]
        [InlineData(int.MaxValue)]
        public void CheckAbiVersion_ProbeReturnsAnotherVersion_IsAbiMismatch(int reported)
        {
            Assert.Equal(NativeLoadResult.AbiMismatch, NativeKernelLoader.CheckAbiVersion(() => reported, 2));
        }

        [Fact]
        public void CheckAbiVersion_LibraryHasNoVersionExport_IsAbiMismatch()
        {
            // What a library built before the version function existed does when the bridge probes it.
            var result = NativeKernelLoader.CheckAbiVersion(
                () => throw new EntryPointNotFoundException("Viewshed_AbiVersion"), 2);

            Assert.Equal(NativeLoadResult.AbiMismatch, result);
        }

        public static TheoryData<Exception> OtherProbeFailures => new()
        {
            new DllNotFoundException(),
            new BadImageFormatException(),
            new SEHException(),
            new InvalidOperationException()
        };

        [Theory]
        [MemberData(nameof(OtherProbeFailures))]
        public void CheckAbiVersion_ProbeFailsAnyOtherWay_IsUnavailable(Exception failure)
        {
            Assert.Equal(NativeLoadResult.Unavailable, NativeKernelLoader.CheckAbiVersion(() => throw failure, 2));
        }

        [Fact]
        public void Load_LibraryThatDoesNotExist_IsUnavailableAndNeverRunsTheProbe()
        {
            var probed = false;

            var result = NativeKernelLoader.Load("no_such_kernel", () => { probed = true; return 2; }, 2);

            Assert.Equal(NativeLoadResult.Unavailable, result);
            Assert.False(probed);
        }

        [Fact]
        public void LogAbiMismatch_WarnsWithTheLibraryAndTheRebuildHint()
        {
            var logger = new Repositories.CapturingLogger<NativeKernelLoaderTests>();

            NativeKernelLoader.LogAbiMismatch(logger, "viewshed_kernel", 2);

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("viewshed_kernel", entry.Message);
            Assert.Contains("rebuild the native kernels", entry.Message);
        }

        // The built kernels, when present, report the version the bridges were written against; the
        // bridge availability checks below also prove it, but this names the number.

        [Fact]
        public void NativeViewshedKernel_ReportsTheExpectedAbiVersion()
        {
            Assert.SkipUnless(ViewshedNativeBridge.IsAvailable, "native library not loaded");

            Assert.Equal(ViewshedNativeInterop.ExpectedAbiVersion, ViewshedNativeInterop.AbiVersion());
        }

        [Fact]
        public void NativeRasterTerrainKernel_ReportsTheExpectedAbiVersion()
        {
            Assert.SkipUnless(RasterTerrainNativeBridge.IsAvailable, "native library not loaded");

            Assert.Equal(RasterTerrainNativeInterop.ExpectedAbiVersion, RasterTerrainNativeInterop.AbiVersion());
        }

        [Fact]
        public void NativeScoringKernel_ReportsTheExpectedAbiVersion()
        {
            Assert.SkipUnless(NativeScoringBridge.IsAvailable, "native library not loaded");

            Assert.Equal(NativeScoringInterop.ExpectedAbiVersion, NativeScoringInterop.AbiVersion());
        }
    }
}
