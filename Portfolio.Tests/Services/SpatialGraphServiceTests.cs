using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    public class SpatialGraphServiceTests
    {
        [Fact]
        public async Task SpatialGraphRoute_ValidGraph_ReturnsShortestPath()
        {
            // Arrange
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var request = new RouteRequestDto
            {
                StartNodeId = 1,
                EndNodeId = 3,
                Nodes =
                [
                    new GraphNodeDto { Id = 1, Latitude = 0, Longitude = 0 },
                    new GraphNodeDto { Id = 2, Latitude = 0, Longitude = 1 },
                    new GraphNodeDto { Id = 3, Latitude = 0, Longitude = 2 }
                ],
                Edges =
                [
                    new GraphEdgeDto { FromNodeId = 1, ToNodeId = 3, Cost = 10 },
                    new GraphEdgeDto { FromNodeId = 1, ToNodeId = 2, Cost = 2 },
                    new GraphEdgeDto { FromNodeId = 2, ToNodeId = 3, Cost = 2 }
                ]
            };

            // Act
            var result = await service.FindShortestPathAsync(request);

            // Assert
            Assert.True(result.Found);
            Assert.Equal(4, result.TotalCost);
            Assert.Equal([1, 2, 3], result.NodeIds);
            Assert.Equal(3, result.Path.Count);
        }

        [Fact]
        public async Task SpatialGraphServiceArea_ValidGraph_ReturnsReachableNodesOrderedById()
        {
            // Arrange
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var request = new ServiceAreaRequestDto
            {
                OriginNodeId = 1,
                MaxCost = 3,
                Nodes =
                [
                    new GraphNodeDto { Id = 1, Latitude = 0, Longitude = 0 },
                    new GraphNodeDto { Id = 2, Latitude = 0, Longitude = 1 },
                    new GraphNodeDto { Id = 3, Latitude = 0, Longitude = 2 }
                ],
                Edges =
                [
                    new GraphEdgeDto { FromNodeId = 1, ToNodeId = 2, Cost = 2 },
                    new GraphEdgeDto { FromNodeId = 2, ToNodeId = 3, Cost = 2 }
                ]
            };

            // Act
            var result = await service.ComputeServiceAreaAsync(request);

            // Assert
            Assert.Equal([1, 2], result.ReachableNodeIds);
        }

        [Fact]
        public async Task SpatialGraphRoute_NonFiniteNodeCoordinate_ThrowsArgumentException()
        {
            // Arrange
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var request = new RouteRequestDto
            {
                StartNodeId = 1,
                EndNodeId = 2,
                Nodes =
                [
                    new GraphNodeDto { Id = 1, Latitude = double.PositiveInfinity, Longitude = 0 },
                    new GraphNodeDto { Id = 2, Latitude = 0, Longitude = 1 }
                ],
                Edges = [new GraphEdgeDto { FromNodeId = 1, ToNodeId = 2, Cost = 1 }]
            };

            // Act / Assert
            await Assert.ThrowsAsync<ArgumentException>(() => service.FindShortestPathAsync(request));
        }

        // ── Route Planner validation ────────────────────────────────────────

        [Fact]
        public async Task SpatialGraphRoute_NullRequest_ThrowsArgumentNullException()
        {
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.FindShortestPathAsync(null!));
        }

        [Fact]
        public async Task SpatialGraphRoute_MissingStartNode_ThrowsArgumentException()
        {
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var request = new RouteRequestDto
            {
                StartNodeId = 99, EndNodeId = 2,
                Nodes = [new GraphNodeDto { Id = 1 }, new GraphNodeDto { Id = 2 }],
                Edges = [new GraphEdgeDto { FromNodeId = 1, ToNodeId = 2, Cost = 1 }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.FindShortestPathAsync(request));
        }

        [Fact]
        public async Task SpatialGraphRoute_NoPathExists_ReturnsFalse()
        {
            // Arrange — disconnected graph
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var request = new RouteRequestDto
            {
                StartNodeId = 1, EndNodeId = 3,
                Nodes =
                [
                    new GraphNodeDto { Id = 1, Latitude = 0, Longitude = 0 },
                    new GraphNodeDto { Id = 2, Latitude = 0, Longitude = 1 },
                    new GraphNodeDto { Id = 3, Latitude = 0, Longitude = 2 }
                ],
                Edges = [new GraphEdgeDto { FromNodeId = 1, ToNodeId = 2, Cost = 1 }]   // no edge to 3
            };

            var result = await service.FindShortestPathAsync(request);

            Assert.False(result.Found);
            Assert.Empty(result.NodeIds);
        }

        [Fact]
        public async Task SpatialGraphServiceArea_ZeroMaxCost_ReturnsOnlyOrigin()
        {
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var request = new ServiceAreaRequestDto
            {
                OriginNodeId = 1, MaxCost = 0,
                Nodes =
                [
                    new GraphNodeDto { Id = 1, Latitude = 0, Longitude = 0 },
                    new GraphNodeDto { Id = 2, Latitude = 0, Longitude = 1 }
                ],
                Edges = [new GraphEdgeDto { FromNodeId = 1, ToNodeId = 2, Cost = 1 }]
            };

            var result = await service.ComputeServiceAreaAsync(request);

            Assert.Equal([1], result.ReachableNodeIds);
        }

        [Fact]
        public async Task SpatialGraphServiceArea_NegativeMaxCost_ThrowsArgumentException()
        {
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var request = new ServiceAreaRequestDto
            {
                OriginNodeId = 1, MaxCost = -1,
                Nodes = [new GraphNodeDto { Id = 1 }],
                Edges = []
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeServiceAreaAsync(request));
        }

        [Fact]
        public async Task SpatialGraphRoute_SameStartAndEnd_ReturnsSingleNodePath()
        {
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var request = new RouteRequestDto
            {
                StartNodeId = 1, EndNodeId = 1,
                Nodes =
                [
                    new GraphNodeDto { Id = 1, Latitude = 34, Longitude = -117 },
                    new GraphNodeDto { Id = 2, Latitude = 34, Longitude = -116 }
                ],
                Edges = [new GraphEdgeDto { FromNodeId = 1, ToNodeId = 2, Cost = 5 }]
            };

            var result = await service.FindShortestPathAsync(request);

            Assert.True(result.Found);
            Assert.Equal(0, result.TotalCost);
            Assert.Equal([1], result.NodeIds);
        }

        // ── Redlands graph end-to-end pipeline ──────────────────────────────────

        [Fact]
        public async Task SpatialGraphService_GetRedlandsGraph_ReturnsDenseNetworkWithEsriHqDestination()
        {
            // Arrange
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);

            // Act
            var graph = await service.GetRedlandsGraphAsync();

            // Assert — data-agnostic (the network is generated from OpenStreetMap, so exact
            // counts change on regeneration): a non-trivial network whose destination is Esri HQ.
            Assert.True(graph.Nodes.Count >= 100, $"Expected a dense network but got {graph.Nodes.Count} nodes.");
            Assert.True(graph.Edges.Count >= 150, $"Expected >=150 edges but got {graph.Edges.Count}.");
            var hq = Assert.Single(graph.Nodes, n => n.Id == graph.DestinationNodeId);
            Assert.Contains("Esri HQ", hq.Label, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SpatialGraphService_GetRedlandsGraph_ReturnsSameInstanceOnSecondCall()
        {
            // The graph is built once and cached — both calls must return the exact
            // same object reference (verifies the static cache is working).
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var first  = await service.GetRedlandsGraphAsync();
            var second = await service.GetRedlandsGraphAsync();

            Assert.Same(first, second);
        }

        [Fact]
        public async Task SpatialGraphService_DijkstraOnRedlandsGraph_ReachesEsriHqFromRowA()
        {
            // Full round-trip: fetch the Redlands graph, route from node 1 (Row A,
            // far-west corner) to Esri HQ (105) with Dijkstra.
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var graph   = await service.GetRedlandsGraphAsync();

            var request = new RouteRequestDto
            {
                StartNodeId = 1,
                EndNodeId   = 105,
                Algorithm   = "dijkstra",
                Nodes = graph.Nodes,
                Edges = graph.Edges
            };

            var result = await service.FindShortestPathAsync(request);

            Assert.True(result.Found);
            Assert.Equal(1,   result.NodeIds.First());
            Assert.Equal(105, result.NodeIds.Last());
            Assert.Equal("dijkstra", result.AlgorithmUsed);
            Assert.True(result.TotalCost > 0);
            Assert.True(result.DistanceKm > 0);
            Assert.True(result.EstimatedMinutes > 0);
            Assert.True(result.ExploredNodes > 0);
        }

        [Fact]
        public async Task SpatialGraphService_AStarOnRedlandsGraph_ReachesEsriHqFromRowA()
        {
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var graph   = await service.GetRedlandsGraphAsync();

            var request = new RouteRequestDto
            {
                StartNodeId = 1,
                EndNodeId   = 105,
                Algorithm   = "astar",
                Nodes = graph.Nodes,
                Edges = graph.Edges
            };

            var result = await service.FindShortestPathAsync(request);

            Assert.True(result.Found);
            Assert.Equal(1,   result.NodeIds.First());
            Assert.Equal(105, result.NodeIds.Last());
            Assert.Equal("astar", result.AlgorithmUsed);
        }

        [Fact]
        public async Task SpatialGraphService_AStarExploresFewerNodesThanDijkstraOnRedlandsGraph()
        {
            // A* uses a spatial heuristic — it should settle fewer nodes than
            // Dijkstra when routing across the full 109-node grid.
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var graph   = await service.GetRedlandsGraphAsync();

            var dijkReq = new RouteRequestDto { StartNodeId = 1, EndNodeId = 105, Algorithm = "dijkstra", Nodes = graph.Nodes, Edges = graph.Edges };
            var astarReq = new RouteRequestDto { StartNodeId = 1, EndNodeId = 105, Algorithm = "astar",   Nodes = graph.Nodes, Edges = graph.Edges };

            var dijkResult  = await service.FindShortestPathAsync(dijkReq);
            var astarResult = await service.FindShortestPathAsync(astarReq);

            Assert.True(astarResult.ExploredNodes < dijkResult.ExploredNodes,
                $"A* explored {astarResult.ExploredNodes} nodes vs Dijkstra's {dijkResult.ExploredNodes} — A* should be fewer.");
        }

        [Fact]
        public async Task SpatialGraphService_DijkstraAndAStarProduceSameTotalCostOnSimpleGraph()
        {
            // Use a simple deterministic graph — both algorithms must produce the
            // identical optimal cost of 4 (path 1→2→3, cost 2+2, not 1→3 direct cost 10).
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var nodes = new List<GraphNodeDto>
            {
                new() { Id = 1, Latitude = 34.0,   Longitude = -117.3 },
                new() { Id = 2, Latitude = 34.0,   Longitude = -117.2 },
                new() { Id = 3, Latitude = 34.056, Longitude = -117.196 } // Esri HQ-like position
            };
            var edges = new List<GraphEdgeDto>
            {
                new() { FromNodeId = 1, ToNodeId = 3, Cost = 10,  Bidirectional = true },
                new() { FromNodeId = 1, ToNodeId = 2, Cost = 2,   Bidirectional = true },
                new() { FromNodeId = 2, ToNodeId = 3, Cost = 2,   Bidirectional = true }
            };

            var dijkResult  = await service.FindShortestPathAsync(new RouteRequestDto { StartNodeId = 1, EndNodeId = 3, Algorithm = "dijkstra", Nodes = nodes, Edges = edges });
            var astarResult = await service.FindShortestPathAsync(new RouteRequestDto { StartNodeId = 1, EndNodeId = 3, Algorithm = "astar",    Nodes = nodes, Edges = edges });

            Assert.Equal(4.0, dijkResult.TotalCost,  precision: 2);
            Assert.Equal(4.0, astarResult.TotalCost, precision: 2);
        }

        [Fact]
        public async Task SpatialGraphService_AStarOnRedlandsGraph_FindsValidRouteFromNode1To105()
        {
            // The edge costs in the Redlands graph include a ×1.3 detour factor
            // while the A* heuristic uses raw haversine — this makes the heuristic
            // inadmissible, so A* may not always find the globally optimal cost.
            // What we can assert: A* finds a connected, non-empty path that
            // starts at the correct origin and ends at Esri HQ.
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var graph   = await service.GetRedlandsGraphAsync();

            var result = await service.FindShortestPathAsync(new RouteRequestDto
            {
                StartNodeId = 1, EndNodeId = 105, Algorithm = "astar",
                Nodes = graph.Nodes, Edges = graph.Edges
            });

            Assert.True(result.Found);
            Assert.Equal(1,   result.NodeIds.First());
            Assert.Equal(105, result.NodeIds.Last());
            Assert.True(result.TotalCost > 0);
            Assert.Equal("astar", result.AlgorithmUsed);
        }

        [Fact]
        public async Task SpatialGraphService_RouteMetrics_PathCoordinateCountMatchesNodeIdCount()
        {
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var graph   = await service.GetRedlandsGraphAsync();

            var result = await service.FindShortestPathAsync(new RouteRequestDto
            {
                StartNodeId = 61, EndNodeId = 105, Algorithm = "astar",
                Nodes = graph.Nodes, Edges = graph.Edges
            });

            Assert.Equal(result.NodeIds.Count, result.Path.Count);
        }

        [Fact]
        public async Task SpatialGraphService_RouteMetrics_EstimatedMinutesConsistentWithDistanceKm()
        {
            // EstimatedMinutes ≈ DistanceKm / 40 km/h × 60 min — allow 5% tolerance.
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var graph   = await service.GetRedlandsGraphAsync();

            var result = await service.FindShortestPathAsync(new RouteRequestDto
            {
                StartNodeId = 1, EndNodeId = 105, Algorithm = "dijkstra",
                Nodes = graph.Nodes, Edges = graph.Edges
            });

            var expectedMinutes = result.DistanceKm / 40.0 * 60.0;
            Assert.InRange(result.EstimatedMinutes, expectedMinutes * 0.95, expectedMinutes * 1.05);
        }

        [Fact]
        public async Task SpatialGraphService_ServiceAreaOnRedlandsGraph_IncludesOriginAndNearbyNodes()
        {
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var graph   = await service.GetRedlandsGraphAsync();

            var result = await service.ComputeServiceAreaAsync(new ServiceAreaRequestDto
            {
                OriginNodeId = 50, // Colton Ave & State St — directly connected to HQ
                MaxCost = 1.5,
                Nodes = graph.Nodes,
                Edges = graph.Edges
            });

            Assert.Contains(50, result.ReachableNodeIds);
            Assert.True(result.ReachableNodeIds.Count > 1,
                "A 1.5 km service area from a well-connected node should reach more than just the origin.");
        }

        [Fact]
        public async Task SpatialGraphService_ServiceAreaOnRedlandsGraph_ResultIsOrderedById()
        {
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var graph   = await service.GetRedlandsGraphAsync();

            var result = await service.ComputeServiceAreaAsync(new ServiceAreaRequestDto
            {
                OriginNodeId = 50, MaxCost = 2.0,
                Nodes = graph.Nodes, Edges = graph.Edges
            });

            Assert.Equal(result.ReachableNodeIds.OrderBy(id => id).ToList(), result.ReachableNodeIds);
        }

        // ── Directed-edge correctness ────────────────────────────────────────────

        [Fact]
        public async Task SpatialGraphRoute_DirectedEdge_CannotTraverseInReverseDirection()
        {
            // One-way edge: 1 → 2 only. Route 2 → 1 must return not-found.
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var request = new RouteRequestDto
            {
                StartNodeId = 2, EndNodeId = 1,
                Nodes =
                [
                    new GraphNodeDto { Id = 1, Latitude = 34.0, Longitude = -117.0 },
                    new GraphNodeDto { Id = 2, Latitude = 34.0, Longitude = -117.1 }
                ],
                Edges = [new GraphEdgeDto { FromNodeId = 1, ToNodeId = 2, Cost = 1.0, Bidirectional = false }]
            };

            var result = await service.FindShortestPathAsync(request);

            Assert.False(result.Found);
        }

        [Fact]
        public async Task SpatialGraphRoute_BidirectionalEdge_CanTraverseBothDirections()
        {
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var nodes = new List<GraphNodeDto>
            {
                new() { Id = 1, Latitude = 34.0, Longitude = -117.0 },
                new() { Id = 2, Latitude = 34.0, Longitude = -117.1 }
            };
            var edges = new List<GraphEdgeDto>
            {
                new() { FromNodeId = 1, ToNodeId = 2, Cost = 1.0, Bidirectional = true }
            };

            var fwd = await service.FindShortestPathAsync(new RouteRequestDto { StartNodeId = 1, EndNodeId = 2, Nodes = nodes, Edges = edges });
            var rev = await service.FindShortestPathAsync(new RouteRequestDto { StartNodeId = 2, EndNodeId = 1, Nodes = nodes, Edges = edges });

            Assert.True(fwd.Found);
            Assert.True(rev.Found);
            Assert.Equal(fwd.TotalCost, rev.TotalCost, precision: 2);
        }

        // ── Algorithm case-insensitivity ────────────────────────────────────────

        [Theory]
        [InlineData("DIJKSTRA")]
        [InlineData("Dijkstra")]
        [InlineData("ASTAR")]
        [InlineData("AStar")]
        public async Task SpatialGraphRoute_AlgorithmNameIsCaseInsensitive(string algorithm)
        {
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var request = new RouteRequestDto
            {
                StartNodeId = 1, EndNodeId = 3,
                Algorithm = algorithm,
                Nodes =
                [
                    new GraphNodeDto { Id = 1, Latitude = 34.0, Longitude = -117.3 },
                    new GraphNodeDto { Id = 2, Latitude = 34.0, Longitude = -117.2 },
                    new GraphNodeDto { Id = 3, Latitude = 34.0, Longitude = -117.1 }
                ],
                Edges =
                [
                    new GraphEdgeDto { FromNodeId = 1, ToNodeId = 2, Cost = 1.0, Bidirectional = true },
                    new GraphEdgeDto { FromNodeId = 2, ToNodeId = 3, Cost = 1.0, Bidirectional = true }
                ]
            };

            var result = await service.FindShortestPathAsync(request);

            Assert.True(result.Found);
        }

        // ── Cancellation token ───────────────────────────────────────────────────

        [Fact]
        public async Task SpatialGraphService_GetRedlandsGraph_CancelledToken_ThrowsOperationCanceled()
        {
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.GetRedlandsGraphAsync(cts.Token));
        }

        // ── Edges to undeclared nodes (managed Dijkstra / A*) ───────────────────

        private static RouteRequestDto UndeclaredEdgeRoute(string algorithm) => new()
        {
            StartNodeId = 1, EndNodeId = 3, Algorithm = algorithm,
            Nodes =
            [
                new() { Id = 1, Latitude = 34.0, Longitude = -117.3 },
                new() { Id = 2, Latitude = 34.0, Longitude = -117.2 },
                new() { Id = 3, Latitude = 34.056, Longitude = -117.196 }
            ],
            Edges =
            [
                new() { FromNodeId = 1, ToNodeId = 99, Cost = 1, Bidirectional = false },
                new() { FromNodeId = 1, ToNodeId = 2, Cost = 2, Bidirectional = true },
                new() { FromNodeId = 2, ToNodeId = 3, Cost = 2, Bidirectional = true }
            ]
        };

        [Theory]
        [InlineData("dijkstra")]
        [InlineData("astar")]
        public async Task SpatialGraphService_EdgeToUndeclaredNode_IsSkippedAndRouteIsReturned(string algorithm)
        {
            // WP-06 (D-04b): undeclared edges are dropped, matching the native kernel's CSR build.
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);

            var result = await service.FindShortestPathAsync(UndeclaredEdgeRoute(algorithm));

            Assert.True(result.Found);
            Assert.Equal([1, 2, 3], result.NodeIds);
            Assert.Equal(4.0, result.TotalCost, precision: 2);
        }
    }
}
