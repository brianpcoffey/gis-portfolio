using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    // Covers the graph-engine extensions added for the vehicle-routing and
    // emergency-response projects: one-to-all distances, the many-to-many cost matrix,
    // and nearest-node snapping — plus a regression guard for the phantom-node defect
    // (an edge naming a node absent from the node array used to be reported as reachable
    // at zero cost by the native kernel, and threw in the managed path).
    //
    // Runs against the managed fallback, since the suite executes with no native library.
    public class SpatialGraphExtensionsTests
    {
        private static SpatialGraphService NewService() =>
            new(NullLogger<SpatialGraphService>.Instance);

        // 1 --1-- 2 --1-- 3 --1-- 4          5 hangs off 1 at cost 5
        //  \                                 6 is isolated (no edges)
        //   --5-- 5
        private static RoadGraphDto SmallGraph()
        {
            return new RoadGraphDto
            {
                GraphName = "test",
                DestinationNodeId = 4,
                Nodes =
                [
                    new() { Id = 1, Latitude = 34.00, Longitude = -117.00, Label = "n1" },
                    new() { Id = 2, Latitude = 34.01, Longitude = -117.00, Label = "n2" },
                    new() { Id = 3, Latitude = 34.02, Longitude = -117.00, Label = "n3" },
                    new() { Id = 4, Latitude = 34.03, Longitude = -117.00, Label = "n4" },
                    new() { Id = 5, Latitude = 34.00, Longitude = -117.01, Label = "n5" },
                    new() { Id = 6, Latitude = 34.50, Longitude = -117.50, Label = "n6-isolated" }
                ],
                Edges =
                [
                    new() { FromNodeId = 1, ToNodeId = 2, Cost = 1, Bidirectional = true },
                    new() { FromNodeId = 2, ToNodeId = 3, Cost = 1, Bidirectional = true },
                    new() { FromNodeId = 3, ToNodeId = 4, Cost = 1, Bidirectional = true },
                    new() { FromNodeId = 1, ToNodeId = 5, Cost = 5, Bidirectional = true }
                ]
            };
        }

        // ── One-to-all distances ────────────────────────────────────────────────

        [Fact]
        public async Task ComputeDistances_SmallGraph_ReturnsCostsParallelToNodeList()
        {
            var service = NewService();
            var graph = SmallGraph();

            var distances = await service.ComputeDistancesAsync(graph, 1);

            Assert.Equal(graph.Nodes.Count, distances.Length);
            Assert.Equal(0, distances[0]);   // node 1
            Assert.Equal(1, distances[1]);   // node 2
            Assert.Equal(2, distances[2]);   // node 3
            Assert.Equal(3, distances[3]);   // node 4
            Assert.Equal(5, distances[4]);   // node 5
        }

        [Fact]
        public async Task ComputeDistances_OriginIsZeroCost()
        {
            var service = NewService();
            var graph = SmallGraph();

            var distances = await service.ComputeDistancesAsync(graph, 3);

            var index = graph.Nodes.ToList().FindIndex(n => n.Id == 3);
            Assert.Equal(0, distances[index]);
        }

        [Fact]
        public async Task ComputeDistances_DisconnectedNode_ReturnsInfinity()
        {
            var service = NewService();
            var graph = SmallGraph();

            var distances = await service.ComputeDistancesAsync(graph, 1);

            Assert.True(double.IsInfinity(distances[5]));   // node 6 has no edges
        }

        [Fact]
        public async Task ComputeDistances_IsIndexAlignedNotIdAligned()
        {
            // Node ids here are deliberately non-contiguous and unsorted, so an
            // implementation that indexed by id would produce a different answer.
            var service = NewService();
            var graph = new RoadGraphDto
            {
                GraphName = "sparse ids",
                DestinationNodeId = 70,
                Nodes =
                [
                    new() { Id = 70, Latitude = 34.02, Longitude = -117.00, Label = "far" },
                    new() { Id = 10, Latitude = 34.00, Longitude = -117.00, Label = "origin" },
                    new() { Id = 40, Latitude = 34.01, Longitude = -117.00, Label = "middle" }
                ],
                Edges =
                [
                    new() { FromNodeId = 10, ToNodeId = 40, Cost = 2, Bidirectional = true },
                    new() { FromNodeId = 40, ToNodeId = 70, Cost = 3, Bidirectional = true }
                ]
            };

            var distances = await service.ComputeDistancesAsync(graph, 10);

            Assert.Equal(5, distances[0]);   // node 70, listed first
            Assert.Equal(0, distances[1]);   // node 10, the origin
            Assert.Equal(2, distances[2]);   // node 40
        }

        [Fact]
        public async Task ComputeDistances_UnknownOrigin_ThrowsArgumentException()
        {
            var service = NewService();
            await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeDistancesAsync(SmallGraph(), 999));
        }

        [Fact]
        public async Task ComputeDistances_NullGraph_ThrowsArgumentNullException()
        {
            var service = NewService();
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.ComputeDistancesAsync(null!, 1));
        }

        // ── Distance matrix ─────────────────────────────────────────────────────

        [Fact]
        public async Task ComputeDistanceMatrix_IsRowMajor()
        {
            var service = NewService();
            int[] sources = [1, 3];
            int[] targets = [2, 4];

            var matrix = await service.ComputeDistanceMatrixAsync(SmallGraph(), sources, targets);

            Assert.Equal(4, matrix.Length);
            Assert.Equal(1, matrix[0]);   // 1 -> 2
            Assert.Equal(3, matrix[1]);   // 1 -> 4
            Assert.Equal(1, matrix[2]);   // 3 -> 2
            Assert.Equal(1, matrix[3]);   // 3 -> 4
        }

        [Fact]
        public async Task ComputeDistanceMatrix_MatchesPairwiseShortestPaths()
        {
            var service = NewService();
            var graph = SmallGraph();
            int[] ids = [1, 2, 3, 4, 5];

            var matrix = await service.ComputeDistanceMatrixAsync(graph, ids, ids);

            for (var s = 0; s < ids.Length; s++)
            {
                for (var t = 0; t < ids.Length; t++)
                {
                    var route = await service.FindShortestPathAsync(new RouteRequestDto
                    {
                        Nodes = graph.Nodes,
                        Edges = graph.Edges,
                        StartNodeId = ids[s],
                        EndNodeId = ids[t],
                        Algorithm = "dijkstra"
                    });

                    Assert.True(route.Found);
                    Assert.Equal(route.TotalCost, matrix[s * ids.Length + t], 6);
                }
            }
        }

        [Fact]
        public async Task ComputeDistanceMatrix_UnreachableTarget_ReturnsInfinity()
        {
            var service = NewService();
            var matrix = await service.ComputeDistanceMatrixAsync(SmallGraph(), [1], [6]);

            Assert.True(double.IsInfinity(matrix[0]));
        }

        [Fact]
        public async Task ComputeDistanceMatrix_ExceedsCellLimit_ThrowsArgumentException()
        {
            var service = NewService();
            // 501 x 501 = 251,001 cells, above the 250,000 ceiling. Ids may repeat —
            // validation checks existence, not uniqueness.
            var sources = Enumerable.Repeat(1, 501).ToList();
            var targets = Enumerable.Repeat(2, 501).ToList();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.ComputeDistanceMatrixAsync(SmallGraph(), sources, targets));
        }

        [Fact]
        public async Task ComputeDistanceMatrix_UnknownSource_ThrowsArgumentException()
        {
            var service = NewService();
            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.ComputeDistanceMatrixAsync(SmallGraph(), [999], [1]));
        }

        [Fact]
        public async Task ComputeDistanceMatrix_EmptySources_ThrowsArgumentException()
        {
            var service = NewService();
            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.ComputeDistanceMatrixAsync(SmallGraph(), [], [1]));
        }

        // ── Phantom-node regression ─────────────────────────────────────────────

        private static RoadGraphDto GraphWithDanglingEdge()
        {
            var graph = SmallGraph();
            // Node 99 is never declared. Relaxing this edge used to insert a zero-cost
            // phantom entry natively, and throw KeyNotFoundException in managed code.
            graph.Edges = [.. graph.Edges, new GraphEdgeDto { FromNodeId = 4, ToNodeId = 99, Cost = 1, Bidirectional = true }];
            return graph;
        }

        [Fact]
        public async Task ComputeDistances_EdgeReferencingMissingNode_DoesNotThrowOrCorruptCosts()
        {
            var service = NewService();

            var distances = await service.ComputeDistancesAsync(GraphWithDanglingEdge(), 1);

            Assert.Equal(6, distances.Length);
            Assert.Equal(0, distances[0]);
            Assert.Equal(3, distances[3]);
            Assert.True(double.IsInfinity(distances[5]));
        }

        [Fact]
        public async Task ServiceArea_EdgeReferencingMissingNode_ExcludesPhantomNode()
        {
            var service = NewService();

            var result = await service.ComputeServiceAreaAsync(new ServiceAreaRequestDto
            {
                Nodes = GraphWithDanglingEdge().Nodes,
                Edges = GraphWithDanglingEdge().Edges,
                OriginNodeId = 1,
                MaxCost = 100
            });

            Assert.DoesNotContain(99, result.ReachableNodeIds);
            Assert.All(result.ReachableNodeIds, id => Assert.InRange(id, 1, 6));
        }

        [Fact]
        public async Task FindShortestPath_EdgeReferencingMissingNode_StillRoutes()
        {
            var service = NewService();

            var route = await service.FindShortestPathAsync(new RouteRequestDto
            {
                Nodes = GraphWithDanglingEdge().Nodes,
                Edges = GraphWithDanglingEdge().Edges,
                StartNodeId = 1,
                EndNodeId = 4,
                Algorithm = "dijkstra"
            });

            Assert.True(route.Found);
            Assert.Equal(3, route.TotalCost, 6);
        }

        // ── Nearest-node snapping ───────────────────────────────────────────────

        [Fact]
        public void SnapToNearestNode_ExactNodeCoordinate_ReturnsThatNode()
        {
            var service = NewService();
            Assert.Equal(3, service.SnapToNearestNode(SmallGraph(), 34.02, -117.00));
        }

        [Fact]
        public void SnapToNearestNode_ReturnsClosestByHaversine()
        {
            var service = NewService();
            // ~0.11 km from node 1, ~1.0 km from node 2.
            Assert.Equal(1, service.SnapToNearestNode(SmallGraph(), 34.001, -117.000));
        }

        [Fact]
        public void SnapToNearestNode_SnapsToIsolatedNodeWhenNearest()
        {
            // Snapping is purely geometric — it does not consider connectivity.
            var service = NewService();
            Assert.Equal(6, service.SnapToNearestNode(SmallGraph(), 34.49, -117.49));
        }

        [Fact]
        public void SnapToNearestNode_NonFiniteCoordinate_ThrowsArgumentException()
        {
            var service = NewService();
            Assert.Throws<ArgumentException>(() => service.SnapToNearestNode(SmallGraph(), double.NaN, -117.0));
        }

        [Fact]
        public void SnapToNearestNode_NullGraph_ThrowsArgumentNullException()
        {
            var service = NewService();
            Assert.Throws<ArgumentNullException>(() => service.SnapToNearestNode(null!, 34.0, -117.0));
        }
    }
}
