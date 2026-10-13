using Portfolio.Services.Native;

namespace Portfolio.Tests.Services
{
    // Guards the CI native-present run against passing vacuously. The native-gated tests
    // skip when a kernel did not load, so a broken staging step would otherwise look green.
    // CI sets PORTFOLIO_REQUIRE_NATIVE=1 on that step only; everywhere else this test passes
    // regardless of what loaded.
    public class NativeAvailabilityTests
    {
        private static readonly (string Name, bool Available)[] Bridges =
        {
            ("NativeScoringBridge", NativeScoringBridge.IsAvailable),
            ("GeoStreamNativeBridge", GeoStreamNativeBridge.IsAvailable),
            ("SpatialGeometryNativeBridge", SpatialGeometryNativeBridge.IsAvailable),
            ("RasterTerrainNativeBridge", RasterTerrainNativeBridge.IsAvailable),
            ("SpatialGraphNativeBridge", SpatialGraphNativeBridge.IsAvailable),
            ("SpatialClusterNativeBridge", SpatialClusterNativeBridge.IsAvailable),
            ("ViewshedNativeBridge", ViewshedNativeBridge.IsAvailable),
            ("SpatialOverlayNativeBridge", SpatialOverlayNativeBridge.IsAvailable),
            ("CatRiskNativeBridge", CatRiskNativeBridge.IsAvailable),
            ("ChangeDetectionNativeBridge", ChangeDetectionNativeBridge.IsAvailable),
            ("NetworkTraceNativeBridge", NetworkTraceNativeBridge.IsAvailable),
            ("FacilityLocationNativeBridge", FacilityLocationNativeBridge.IsAvailable),
            ("VrpSolverNativeBridge", VrpSolverNativeBridge.IsAvailable),
        };

        [Fact]
        public void AllThirteenBridges_AreListed()
        {
            Assert.Equal(13, Bridges.Length);
        }

        [Fact]
        public void WhenNativeRequired_EveryBridgeLoaded()
        {
            if (Environment.GetEnvironmentVariable("PORTFOLIO_REQUIRE_NATIVE") != "1")
                return;

            var missing = Bridges.Where(b => !b.Available).Select(b => b.Name).ToArray();
            Assert.True(missing.Length == 0,
                "PORTFOLIO_REQUIRE_NATIVE=1 but these native libraries did not load: " + string.Join(", ", missing));
        }
    }
}
