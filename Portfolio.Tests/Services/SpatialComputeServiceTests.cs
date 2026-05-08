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
            Assert.False(result.NativeAccelerated);
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
            Assert.False(result.NativeAccelerated);
            Assert.Equal(2, result.Triangles.Count);
        }

        [Fact]
        public async Task SpatialGeometryClip_ValidBoundingBox_ReturnsClampedVertices()
        {
            // Arrange
            var service = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
            var request = new PolygonClipRequestDto
            {
                MinX = 0,
                MinY = 0,
                MaxX = 1,
                MaxY = 1,
                Subject =
                [
                    new CoordinateDto { X = -1, Y = 0.5 },
                    new CoordinateDto { X = 0.5, Y = 2 }
                ]
            };

            // Act
            var result = await service.ClipToBoundingBoxAsync(request);

            // Assert
            Assert.False(result.NativeAccelerated);
            Assert.Equal(2, result.Vertices.Count);
            Assert.Equal(0, result.Vertices[0].X);
            Assert.Equal(1, result.Vertices[1].Y);
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
            Assert.False(result.NativeAccelerated);
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
            Assert.False(result.NativeAccelerated);
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
            Assert.False(result.NativeAccelerated);
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
            Assert.False(result.NativeAccelerated);
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
    }
}
