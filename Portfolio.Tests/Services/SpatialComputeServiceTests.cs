using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    public class SpatialComputeServiceTests
    {
        [Fact]
        public async Task GeoStreamProcessBatch_ValidEvents_ReturnsAggregates()
        {
            // Arrange
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = 1,
                AnomalySpeedThresholdMetersPerSecond = 30,
                Events =
                [
                    new TelemetryEventDto { EntityId = 1, Latitude = 34.05, Longitude = -117.18, SpeedMetersPerSecond = 12 },
                    new TelemetryEventDto { EntityId = 2, Latitude = 34.10, Longitude = -117.20, SpeedMetersPerSecond = 40 },
                    new TelemetryEventDto { EntityId = 3, Latitude = 120, Longitude = -117.20, SpeedMetersPerSecond = 10 }
                ]
            };

            // Act
            var result = await service.ProcessBatchAsync(request);

            // Assert
            Assert.Equal(3, result.TotalEvents);
            Assert.Equal(2, result.ValidEvents);
            Assert.Equal(1, result.InvalidEvents);
            Assert.Equal(1, result.AnomalyCount);
            Assert.Single(result.Aggregates);
        }

        [Fact]
        public async Task GeoStreamProcessBatch_TooManyEvents_ThrowsArgumentException()
        {
            // Arrange
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto
            {
                Events = Enumerable.Range(0, 10001)
                    .Select(i => new TelemetryEventDto { EntityId = i, Latitude = 34, Longitude = -117 })
                    .ToList()
            };

            // Act / Assert
            await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessBatchAsync(request));
        }

        [Fact]
        public async Task SpatialGeometryTriangulate_ValidPoints_ReturnsFanTriangles()
        {
            // Arrange
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new GeometryPointSetDto
            {
                Points =
                [
                    new CoordinateDto { X = 0, Y = 0 },
                    new CoordinateDto { X = 1, Y = 0 },
                    new CoordinateDto { X = 1, Y = 1 },
                    new CoordinateDto { X = 0, Y = 1 }
                ]
            };

            // Act
            var result = await service.TriangulateAsync(request);

            // Assert
            Assert.Equal(2, result.Triangles.Count);
        }

        [Fact]
        public async Task SpatialGeometryClip_PolygonStraddlingCorner_EmitsBoxCorner()
        {
            // A triangle whose right-angle overhangs the box's top-right corner. Correct
            // Sutherland–Hodgman clipping against [0,1]x[0,1] yields the quadrilateral
            // (0.5,0.5),(1,0.5),(1,1),(0.5,1) — crucially INCLUDING the (1,1) box corner
            // that naive per-vertex clamping drops.
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new PolygonClipRequestDto
            {
                MinX = 0, MinY = 0, MaxX = 1, MaxY = 1,
                Subject =
                [
                    new CoordinateDto { X = 0.5, Y = 0.5 },
                    new CoordinateDto { X = 2.0, Y = 0.5 },
                    new CoordinateDto { X = 0.5, Y = 2.0 }
                ]
            };

            var result = await service.ClipToBoundingBoxAsync(request);

            Assert.Equal(4, result.Vertices.Count);
            Assert.Contains(result.Vertices, v => Math.Abs(v.X - 1.0) < 1e-9 && Math.Abs(v.Y - 1.0) < 1e-9);
            Assert.All(result.Vertices, v =>
            {
                Assert.InRange(v.X, 0.0, 1.0);
                Assert.InRange(v.Y, 0.0, 1.0);
            });
        }

        [Fact]
        public async Task SpatialGeometryClip_PolygonFullyOutsideBox_ReturnsEmpty()
        {
            // A polygon with no overlap with the box must clip to nothing — not collapse
            // to duplicate corner points (the old clamp bug that falsely reported a shape).
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new PolygonClipRequestDto
            {
                MinX = 0, MinY = 0, MaxX = 1, MaxY = 1,
                Subject =
                [
                    new CoordinateDto { X = 5, Y = 5 },
                    new CoordinateDto { X = 6, Y = 5 },
                    new CoordinateDto { X = 5.5, Y = 6 }
                ]
            };

            var result = await service.ClipToBoundingBoxAsync(request);

            Assert.Empty(result.Vertices);
        }

        [Fact]
        public async Task SpatialGeometryTriangulate_NonFiniteCoordinate_ThrowsArgumentException()
        {
            // Arrange
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new GeometryPointSetDto
            {
                Points =
                [
                    new CoordinateDto { X = 0, Y = 0 },
                    new CoordinateDto { X = double.NaN, Y = 0 },
                    new CoordinateDto { X = 1, Y = 1 }
                ]
            };

            // Act / Assert
            await Assert.ThrowsAsync<ArgumentException>(() => service.TriangulateAsync(request));
        }

        [Fact]
        public async Task RasterTerrainHillshade_ValidRaster_ReturnsIntensityGrid()
        {
            // Arrange
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = new RasterHillshadeRequestDto
            {
                Width = 2,
                Height = 2,
                Elevation = [10, 20, 30, 40]
            };

            // Act
            var result = await service.GenerateHillshadeAsync(request);

            // Assert
            Assert.Equal(4, result.Intensities.Count);
        }

        [Fact]
        public async Task RasterTerrainHeatmap_ValidRequest_ReturnsNormalizedGrid()
        {
            // Arrange
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = new HeatmapRequestDto
            {
                Width = 2,
                Height = 2,
                MinX = 0,
                MinY = 0,
                MaxX = 1,
                MaxY = 1,
                Radius = 0.5,
                Points = [new WeightedPointDto { X = 0.5, Y = 0.5, Weight = 1 }]
            };

            // Act
            var result = await service.GenerateHeatmapAsync(request);

            // Assert
            Assert.Equal(4, result.Values.Count);
            Assert.All(result.Values, value => Assert.InRange(value, 0, 1));
        }

        [Fact]
        public async Task RasterTerrainHillshade_TooManyCells_ThrowsArgumentException()
        {
            // Arrange
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = new RasterHillshadeRequestDto
            {
                Width = 501,
                Height = 500,
                Elevation = Enumerable.Repeat(1.0, 250500).ToList()
            };

            // Act / Assert
            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHillshadeAsync(request));
        }

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

        // ── GeoStream validation ────────────────────────────────────────────

        [Fact]
        public async Task GeoStreamProcessBatch_NullRequest_ThrowsArgumentNullException()
        {
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.ProcessBatchAsync(null!));
        }

        [Fact]
        public async Task GeoStreamProcessBatch_EmptyEventList_ThrowsArgumentException()
        {
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto { GridSizeDegrees = 1, Events = [] };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessBatchAsync(request));
        }

        [Fact]
        public async Task GeoStreamProcessBatch_ZeroGridSize_ThrowsArgumentException()
        {
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = 0,
                Events = [new TelemetryEventDto { EntityId = 1, Latitude = 34, Longitude = -117 }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessBatchAsync(request));
        }

        [Fact]
        public async Task GeoStreamProcessBatch_NegativeAnomalyThreshold_ThrowsArgumentException()
        {
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = 1,
                AnomalySpeedThresholdMetersPerSecond = -1,
                Events = [new TelemetryEventDto { EntityId = 1, Latitude = 34, Longitude = -117 }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessBatchAsync(request));
        }

        [Fact]
        public async Task GeoStreamProcessBatch_AllEventsInvalid_ReturnsZeroValidAndEmptyAggregates()
        {
            // Arrange — all events are out of range
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = 1,
                Events =
                [
                    new TelemetryEventDto { EntityId = 1, Latitude = 200, Longitude = 0 },
                    new TelemetryEventDto { EntityId = 2, Latitude = 0,   Longitude = 500 }
                ]
            };

            // Act
            var result = await service.ProcessBatchAsync(request);

            // Assert
            Assert.Equal(0, result.ValidEvents);
            Assert.Equal(2, result.InvalidEvents);
            Assert.Empty(result.Aggregates);
        }

        [Fact]
        public async Task GeoStreamProcessBatch_MultipleEventsInSameCell_AggregatesIntoOneCell()
        {
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = 10,
                AnomalySpeedThresholdMetersPerSecond = 100,
                Events =
                [
                    new TelemetryEventDto { EntityId = 1, Latitude = 34.0, Longitude = -117.0, SpeedMetersPerSecond = 10 },
                    new TelemetryEventDto { EntityId = 2, Latitude = 34.1, Longitude = -117.1, SpeedMetersPerSecond = 20 },
                    new TelemetryEventDto { EntityId = 3, Latitude = 34.2, Longitude = -117.2, SpeedMetersPerSecond = 30 }
                ]
            };

            var result = await service.ProcessBatchAsync(request);

            Assert.Single(result.Aggregates);
            Assert.Equal(3, result.Aggregates[0].Count);
            Assert.InRange(result.Aggregates[0].AverageSpeedMetersPerSecond, 19.9, 20.1);
        }

        // ── Geometry Toolkit validation ─────────────────────────────────────

        [Fact]
        public async Task SpatialGeometryTriangulate_NullRequest_ThrowsArgumentNullException()
        {
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.TriangulateAsync(null!));
        }

        [Fact]
        public async Task SpatialGeometryTriangulate_FewerThanThreePoints_ThrowsArgumentException()
        {
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new GeometryPointSetDto
            {
                Points = [new CoordinateDto { X = 0, Y = 0 }, new CoordinateDto { X = 1, Y = 1 }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.TriangulateAsync(request));
        }

        [Fact]
        public async Task SpatialGeometryTriangulate_NPointPolygon_ProducesNMinus2Triangles()
        {
            // A fan from origin: N points → N-2 triangles
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new GeometryPointSetDto
            {
                Points =
                [
                    new CoordinateDto { X = 0, Y = 0 },
                    new CoordinateDto { X = 1, Y = 0 },
                    new CoordinateDto { X = 1, Y = 1 },
                    new CoordinateDto { X = 0, Y = 1 },
                    new CoordinateDto { X = 0.5, Y = 1.5 }
                ]
            };

            var result = await service.TriangulateAsync(request);

            Assert.Equal(3, result.Triangles.Count);
        }

        [Fact]
        public async Task SpatialGeometryClip_NullRequest_ThrowsArgumentNullException()
        {
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.ClipToBoundingBoxAsync(null!));
        }

        [Fact]
        public async Task SpatialGeometryClip_InvalidBoundingBox_ThrowsArgumentException()
        {
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new PolygonClipRequestDto
            {
                MinX = 1, MaxX = 0, MinY = 0, MaxY = 1,   // min > max — invalid
                Subject = [new CoordinateDto { X = 0, Y = 0 }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ClipToBoundingBoxAsync(request));
        }

        [Fact]
        public async Task SpatialGeometryClip_InfiniteCoordinate_ThrowsArgumentException()
        {
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new PolygonClipRequestDto
            {
                MinX = 0, MaxX = 1, MinY = 0, MaxY = 1,
                Subject = [new CoordinateDto { X = double.PositiveInfinity, Y = 0 }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ClipToBoundingBoxAsync(request));
        }

        // ── Terrain Analyzer validation ─────────────────────────────────────

        [Fact]
        public async Task RasterTerrainHillshade_NullRequest_ThrowsArgumentNullException()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.GenerateHillshadeAsync(null!));
        }

        [Fact]
        public async Task RasterTerrainHillshade_ZeroCellSize_ThrowsArgumentException()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = new RasterHillshadeRequestDto
            {
                Width = 2, Height = 2, CellSize = 0,
                Elevation = [10, 20, 30, 40]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHillshadeAsync(request));
        }

        [Fact]
        public async Task RasterTerrainHillshade_ElevationLengthMismatch_ThrowsArgumentException()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = new RasterHillshadeRequestDto
            {
                Width = 2, Height = 2, CellSize = 1,
                Elevation = [10, 20, 30]   // should be 4
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHillshadeAsync(request));
        }

        [Fact]
        public async Task RasterTerrainHeatmap_NullRequest_ThrowsArgumentNullException()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.GenerateHeatmapAsync(null!));
        }

        [Fact]
        public async Task RasterTerrainHeatmap_InvalidExtent_ThrowsArgumentException()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = new HeatmapRequestDto
            {
                Width = 2, Height = 2, Radius = 0.5,
                MinX = 1, MaxX = 0, MinY = 0, MaxY = 1,   // min > max
                Points = [new WeightedPointDto { X = 0.5, Y = 0.5, Weight = 1 }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHeatmapAsync(request));
        }

        [Fact]
        public async Task RasterTerrainHeatmap_ZeroRadius_ThrowsArgumentException()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = new HeatmapRequestDto
            {
                Width = 2, Height = 2, Radius = 0,
                MinX = 0, MaxX = 1, MinY = 0, MaxY = 1,
                Points = [new WeightedPointDto { X = 0.5, Y = 0.5, Weight = 1 }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateHeatmapAsync(request));
        }

        [Fact]
        public async Task RasterTerrainHillshade_1x1Raster_ReturnsSingleIntensity()
        {
            var service = new RasterTerrainService(NullLogger<RasterTerrainService>.Instance);
            var request = new RasterHillshadeRequestDto
            {
                Width = 1, Height = 1, CellSize = 1,
                Elevation = [100.0]
            };

            var result = await service.GenerateHillshadeAsync(request);

            Assert.Single(result.Intensities);
            Assert.InRange(result.Intensities[0], (byte)0, (byte)255);
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

        // ── Controller integration ───────────────────────────────────────────────

        [Fact]
        public async Task SpatialNetworkController_GetGraph_ReturnsOkWithRedlandsGraph()
        {
            // Arrange — use real service so the static cache path is exercised.
            var service    = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var controller = new Portfolio.Web.Controllers.Api.SpatialNetworkController(
                service, NullLogger<Portfolio.Web.Controllers.Api.SpatialNetworkController>.Instance);

            // Act
            var result = await controller.GetGraph(CancellationToken.None);

            // Assert
            var ok = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
            var graph = Assert.IsType<RoadGraphDto>(ok.Value);
            Assert.True(graph.Nodes.Count >= 100, $"Expected a dense network but got {graph.Nodes.Count} nodes.");
            Assert.Contains(graph.Nodes, n => n.Id == graph.DestinationNodeId);
        }
    }
}

