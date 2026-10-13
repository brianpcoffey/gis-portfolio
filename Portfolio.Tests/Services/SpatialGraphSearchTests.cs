using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    // Pins behaviour that the shared Dijkstra/A* search and the split ValidateGraph must keep.
    public class SpatialGraphSearchTests
    {
        private static SpatialGraphService NewService() =>
            new(NullLogger<SpatialGraphService>.Instance);

        private static RouteRequestDto SmallGraph(string algorithm) => new()
        {
            Nodes =
            [
                new GraphNodeDto { Id = 1, Latitude = 34.00, Longitude = -117.00 },
                new GraphNodeDto { Id = 2, Latitude = 34.01, Longitude = -117.00 },
                new GraphNodeDto { Id = 3, Latitude = 34.02, Longitude = -117.00 }
            ],
            Edges =
            [
                new GraphEdgeDto { FromNodeId = 1, ToNodeId = 99, Cost = 0.1 },   // undeclared target
                new GraphEdgeDto { FromNodeId = 1, ToNodeId = 2, Cost = 1.2 },
                new GraphEdgeDto { FromNodeId = 2, ToNodeId = 3, Cost = 1.3 }
            ],
            StartNodeId = 1,
            EndNodeId = 3,
            Algorithm = algorithm
        };

        [Theory]
        [InlineData("dijkstra")]
        [InlineData("astar")]
        public async Task UndeclaredEdgeTarget_IsSkipped(string algorithm)
        {
            var result = await NewService().FindShortestPathAsync(SmallGraph(algorithm));

            Assert.True(result.Found);
            Assert.Equal(2.5, result.TotalCost);
            Assert.Equal([1, 2, 3], result.NodeIds);
        }

        [Fact]
        public async Task AStar_ReportsAlgorithmName()
        {
            var result = await NewService().FindShortestPathAsync(SmallGraph("astar"));

            Assert.Equal("astar", result.AlgorithmUsed);
            Assert.Equal(result.ExploredNodeIds.Count, result.ExploredNodes);
        }

        [Fact]
        public async Task Unreachable_EndNode_ReturnsNotFound()
        {
            var request = SmallGraph("astar");
            request.Edges.RemoveAt(2);

            var result = await NewService().FindShortestPathAsync(request);

            Assert.False(result.Found);
            Assert.Equal(0, result.TotalCost);
            Assert.Equal("astar", result.AlgorithmUsed);
        }

        [Fact]
        public async Task Validation_ReportsEdgeCountBeforeNodeCoordinates()
        {
            var request = SmallGraph("dijkstra");
            request.Nodes[0].Latitude = double.NaN;
            request.Edges.Clear();

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => NewService().FindShortestPathAsync(request));

            Assert.Contains("At least one graph edge", ex.Message);
        }

        [Fact]
        public async Task Validation_ReportsNodeCoordinatesBeforeEdgeCosts()
        {
            var request = SmallGraph("dijkstra");
            request.Nodes[0].Latitude = double.NaN;
            request.Edges[1].Cost = -1;

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => NewService().FindShortestPathAsync(request));

            Assert.Contains("coordinates", ex.Message);
        }
    }
}
