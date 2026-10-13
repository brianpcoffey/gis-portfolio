using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Native;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    // WP-53 review fixes for two kernels that were still exposed to the hazards the rest of the
    // work removed:
    //  * spatial_graph_engine tested "is this distance finite?" with std::isfinite, which the
    //    fast-math build is allowed to fold to true, so every unreachable node (distance +infinity)
    //    could count as reached. It now tests the bit pattern, like the geostream and cluster kernels.
    //  * spatial_overlay_kernel added the ring sizes in an int. A sum that wraps could equal
    //    totalVertices, after which the point loop walked rings far larger than the vertex buffer.
    //
    // The tests that call a native entry point report Skipped when the library is absent (the normal
    // clean-clone state), like NativeParityTests.
    public class NativeGraphOverlayAbiTests
    {
        private const string SkipReason = "native library not loaded";

        // ---------------------------------------------------------------------------------------
        // graph: a distance that is not finite is never reported as reached.
        // ---------------------------------------------------------------------------------------

        private static GraphNodeNative[] Nodes(params int[] ids) =>
            ids.Select(id => new GraphNodeNative { Id = id, Latitude = 34 + id * 0.01, Longitude = -117 }).ToArray();

        private static GraphEdgeNative Edge(int from, int to, double cost) =>
            new() { FromNodeId = from, ToNodeId = to, Cost = cost, Bidirectional = 1 };

        private static (int Status, GraphPathResultNative Result) NativePath(GraphNodeNative[] nodes, GraphEdgeNative[] edges, int start, int end)
        {
            var path = new int[nodes.Length];
            var explored = new int[nodes.Length];
            GraphPathResultNative result = default;
            var exploredCount = 0;

            var status = SpatialGraphNativeInterop.FindShortestPath(
                nodes, nodes.Length, edges, edges.Length, start, end, path, path.Length, out result, explored, explored.Length, out exploredCount);

            return (status, result);
        }

        [Fact]
        public void NativeGraph_EndNodeThatCannotBeReached_IsNotFound()
        {
            Assert.SkipUnless(SpatialGraphNativeBridge.IsAvailable, SkipReason);

            // Node 3 is declared but no edge reaches it, so its distance stays +infinity.
            var (status, result) = NativePath(Nodes(1, 2, 3), [Edge(1, 2, 1.5)], start: 1, end: 3);

            Assert.Equal(0, status);
            Assert.Equal(0, result.Found);
            Assert.Equal(0, result.PathCount);
        }

        [Fact]
        public void NativeGraph_Distances_GiveUnreachableNodesInfinityAndCountOnlyTheReachableOnes()
        {
            Assert.SkipUnless(SpatialGraphNativeBridge.IsAvailable, SkipReason);

            var nodes = Nodes(1, 2, 3);
            var edges = new[] { Edge(1, 2, 1.5) };
            var distances = new double[nodes.Length];

            var reachable = SpatialGraphNativeInterop.ComputeDistances(nodes, nodes.Length, edges, edges.Length, 1, distances, distances.Length);

            Assert.Equal(2, reachable);
            Assert.Equal(0.0, distances[0]);
            Assert.Equal(1.5, distances[1]);
            Assert.True(double.IsPositiveInfinity(distances[2]));
        }

        // The managed service refuses a graph with such a cost before it searches (ValidateGraph). The
        // kernel is only ever called with a validated graph, but handed one directly it must not
        // report the node behind the edge as reached either: its distance is not finite.
        public static TheoryData<double> NonFiniteEdgeCosts => new()
        {
            double.PositiveInfinity,
            double.NegativeInfinity,
            double.NaN
        };

        [Theory]
        [MemberData(nameof(NonFiniteEdgeCosts))]
        public async Task SpatialGraphService_EdgeCostThatIsNotFinite_IsRefusedBeforeAnySearch(double cost)
        {
            var request = new RouteRequestDto
            {
                Nodes =
                [
                    new GraphNodeDto { Id = 1, Latitude = 34.00, Longitude = -117.00 },
                    new GraphNodeDto { Id = 2, Latitude = 34.01, Longitude = -117.00 },
                    new GraphNodeDto { Id = 3, Latitude = 34.02, Longitude = -117.00 }
                ],
                Edges =
                [
                    new GraphEdgeDto { FromNodeId = 1, ToNodeId = 2, Cost = 1.0 },
                    new GraphEdgeDto { FromNodeId = 2, ToNodeId = 3, Cost = cost }
                ],
                StartNodeId = 1,
                EndNodeId = 3,
                Algorithm = "dijkstra"
            };

            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => new SpatialGraphService(NullLogger<SpatialGraphService>.Instance).FindShortestPathAsync(request));

            Assert.Contains("finite", ex.Message);
        }

        [Theory]
        [MemberData(nameof(NonFiniteEdgeCosts))]
        public void NativeGraph_EdgeCostThatIsNotFinite_NeverMakesTheNodeBehindItReached(double cost)
        {
            Assert.SkipUnless(SpatialGraphNativeBridge.IsAvailable, SkipReason);

            var nodes = Nodes(1, 2, 3);
            var edges = new[] { Edge(1, 2, 1.0), Edge(2, 3, cost) };

            var (status, result) = NativePath(nodes, edges, start: 1, end: 3);
            var distances = new double[nodes.Length];
            var reachable = SpatialGraphNativeInterop.ComputeDistances(nodes, nodes.Length, edges, edges.Length, 1, distances, distances.Length);

            Assert.Equal(0, status);
            Assert.Equal(0, result.Found);
            Assert.Equal(2, reachable);
            Assert.False(double.IsFinite(distances[2]));
        }

        // ---------------------------------------------------------------------------------------
        // overlay: the ring sizes are summed in 64 bits.
        // ---------------------------------------------------------------------------------------

        private static OverlayPointNative[] Triangle() =>
        [
            new OverlayPointNative { X = 0, Y = 0 },
            new OverlayPointNative { X = 1, Y = 0 },
            new OverlayPointNative { X = 0, Y = 1 }
        ];

        private static readonly OverlayPointNative[] PointInsideTheTriangle = [new OverlayPointNative { X = 0.25, Y = 0.25 }];

        [Fact]
        public void NativeOverlay_ValidZone_AssignsThePoint()
        {
            Assert.SkipUnless(SpatialOverlayNativeBridge.IsAvailable, SkipReason);

            var assignments = new[] { -7 };

            var status = SpatialOverlayNativeInterop.AssignPointsToZones(PointInsideTheTriangle, 1, Triangle(), 3, [3], 1, assignments, 1);

            Assert.Equal(1, status);
            Assert.Equal(0, assignments[0]);
        }

        // 3 + int.MaxValue + int.MaxValue is 2^32 + 1, which is 1 in 32-bit arithmetic: the value of
        // totalVertices below, so a 32-bit accumulator accepted these ring sizes. The first ring is a
        // real triangle that contains the point, so a kernel that did accept them would stop there
        // instead of walking into the two huge rings and reading far past the buffer.
        [Fact]
        public void NativeOverlay_RingSizesWhoseSumWrapsAnInt_AreRejectedBeforeAnythingIsWritten()
        {
            Assert.SkipUnless(SpatialOverlayNativeBridge.IsAvailable, SkipReason);

            var assignments = new[] { -7 };

            var status = SpatialOverlayNativeInterop.AssignPointsToZones(
                PointInsideTheTriangle, 1, Triangle(), 1, [3, int.MaxValue, int.MaxValue], 3, assignments, 1);

            Assert.Equal(-1, status);
            Assert.Equal(-7, assignments[0]);
        }

        [Fact]
        public void NativeOverlay_RingSizesThatDoNotAddUpToTheVertices_KeepTheirOwnStatus()
        {
            Assert.SkipUnless(SpatialOverlayNativeBridge.IsAvailable, SkipReason);

            var assignments = new[] { -7 };

            var status = SpatialOverlayNativeInterop.AssignPointsToZones(PointInsideTheTriangle, 1, Triangle(), 3, [4], 1, assignments, 1);

            Assert.Equal(-4, status);
            Assert.Equal(-7, assignments[0]);
        }

        [Fact]
        public void NativeOverlay_RingWithFewerThanThreeVertices_KeepsItsOwnStatus()
        {
            Assert.SkipUnless(SpatialOverlayNativeBridge.IsAvailable, SkipReason);

            var assignments = new[] { -7 };

            var status = SpatialOverlayNativeInterop.AssignPointsToZones(PointInsideTheTriangle, 1, Triangle(), 3, [2, 1], 2, assignments, 1);

            Assert.Equal(-3, status);
            Assert.Equal(-7, assignments[0]);
        }
    }
}
