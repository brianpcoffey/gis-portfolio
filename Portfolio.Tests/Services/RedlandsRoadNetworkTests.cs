using Portfolio.Common.DTOs;
using Portfolio.Services.Data;

namespace Portfolio.Tests.Services
{
    // Validates the structural integrity and geographic correctness of the
    // pre-built 109-node Redlands / San Bernardino road network.
    public class RedlandsRoadNetworkTests
    {
        // Build once for the whole test class — mirrors the static cache in the service.
        private static readonly RoadGraphDto _graph = RedlandsRoadNetwork.Build();

        // ── Node count and ID uniqueness ────────────────────────────────────────

        [Fact]
        public void Build_ReturnsCorrectNodeCount()
        {
            // 90 grid (rows A-F) + 14 row-G + 1 Esri HQ + 4 freeway ramps = 109
            Assert.Equal(109, _graph.Nodes.Count);
        }

        [Fact]
        public void Build_AllNodeIdsAreUnique()
        {
            var ids = _graph.Nodes.Select(n => n.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Fact]
        public void Build_NodeIdsFormContiguousRangeWithNoGaps()
        {
            // IDs 1-105 (grid + HQ) and 106-109 (ramps) — all 109 are present.
            var idSet = _graph.Nodes.Select(n => n.Id).ToHashSet();
            for (int i = 1; i <= 109; i++)
                Assert.Contains(i, idSet);
        }

        // ── Edge count and minimum connectivity ─────────────────────────────────

        [Fact]
        public void Build_EdgeCountIsAboveMinimumExpected()
        {
            // 90 EW arterial segs + 13 row-G segs + ~90 NS segs + ~35 diagonals
            // + 7 stubs + 9 ramp edges + 5 HQ edges = 239 total.
            Assert.True(_graph.Edges.Count >= 235,
                $"Expected ≥235 edges but got {_graph.Edges.Count}.");
        }

        [Fact]
        public void Build_AllEdgeEndpointsReferenceExistingNodes()
        {
            var nodeIds = _graph.Nodes.Select(n => n.Id).ToHashSet();
            foreach (var edge in _graph.Edges)
            {
                Assert.Contains(edge.FromNodeId, nodeIds);
                Assert.Contains(edge.ToNodeId, nodeIds);
            }
        }

        [Fact]
        public void Build_AllEdgeCostsArePositiveAndFinite()
        {
            foreach (var edge in _graph.Edges)
            {
                Assert.True(edge.Cost > 0 && double.IsFinite(edge.Cost),
                    $"Edge {edge.FromNodeId}→{edge.ToNodeId} has invalid cost {edge.Cost}.");
            }
        }

        [Fact]
        public void Build_NoSelfLoops()
        {
            foreach (var edge in _graph.Edges)
                Assert.NotEqual(edge.FromNodeId, edge.ToNodeId);
        }

        // ── Special node identity ────────────────────────────────────────────────

        [Fact]
        public void Build_DestinationNodeIdIsEsriHq()
        {
            Assert.Equal(RedlandsRoadNetwork.EsriHqNodeId, _graph.DestinationNodeId);
        }

        [Fact]
        public void Build_EsriHqNodeExistsWithCorrectCoordinates()
        {
            var hq = _graph.Nodes.Single(n => n.Id == RedlandsRoadNetwork.EsriHqNodeId);
            Assert.InRange(hq.Latitude,  34.055, 34.058);
            Assert.InRange(hq.Longitude, -117.197, -117.194);
            Assert.Contains("Esri HQ", hq.Label, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Build_EsriHqHasAtLeastFourConnections()
        {
            int hqId = RedlandsRoadNetwork.EsriHqNodeId;
            int connections = _graph.Edges.Count(
                e => e.FromNodeId == hqId || (e.Bidirectional && e.ToNodeId == hqId));
            Assert.True(connections >= 4,
                $"Esri HQ should have ≥4 connections but has {connections}.");
        }

        [Fact]
        public void Build_FreewayRampNodesExist()
        {
            var nodeIds = _graph.Nodes.Select(n => n.Id).ToHashSet();
            foreach (int rampId in new[] { 106, 107, 108, 109 })
                Assert.Contains(rampId, nodeIds);
        }

        [Fact]
        public void Build_FreewayRampNodesHaveWestOfGridLongitudes()
        {
            // All four ramp nodes sit west of column 1 (-117.230).
            foreach (int rampId in new[] { 106, 107, 108, 109 })
            {
                var node = _graph.Nodes.Single(n => n.Id == rampId);
                Assert.True(node.Longitude < -117.230,
                    $"Ramp node {rampId} longitude {node.Longitude} should be west of -117.230.");
            }
        }

        // ── Coordinate sanity ────────────────────────────────────────────────────

        [Fact]
        public void Build_AllNodeCoordinatesAreFinite()
        {
            foreach (var node in _graph.Nodes)
            {
                Assert.True(double.IsFinite(node.Latitude),
                    $"Node {node.Id} has non-finite latitude.");
                Assert.True(double.IsFinite(node.Longitude),
                    $"Node {node.Id} has non-finite longitude.");
            }
        }

        [Fact]
        public void Build_AllNodesAreWithinRedlandsBoundingBox()
        {
            // Generous bounding box: lat 34.03–34.09, lng -117.26–117.05
            foreach (var node in _graph.Nodes)
            {
                Assert.InRange(node.Latitude,  34.03, 34.09);
                Assert.InRange(node.Longitude, -117.26, -117.05);
            }
        }

        // ── Grid row / column label sanity ───────────────────────────────────────

        [Fact]
        public void Build_RowANodeLabelsContainBartonRd()
        {
            // Row A = node IDs 1-15
            var rowA = _graph.Nodes.Where(n => n.Id >= 1 && n.Id <= 15).ToList();
            Assert.All(rowA, n => Assert.Contains("Barton Rd", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_GraphNameContainsNodeCountAndCity()
        {
            Assert.Contains("109", _graph.GraphName, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Redlands", _graph.GraphName, StringComparison.OrdinalIgnoreCase);
        }

        // ── Reachability smoke test via BFS ──────────────────────────────────────

        [Fact]
        public void Build_EsriHqIsReachableFromAllGridRowOrigins()
        {
            // BFS over the full adjacency to confirm HQ is reachable from the
            // first node in each of the six main grid rows.
            var adjacency = new Dictionary<int, List<int>>();
            foreach (var edge in _graph.Edges)
            {
                if (!adjacency.TryGetValue(edge.FromNodeId, out var fwd)) { fwd = []; adjacency[edge.FromNodeId] = fwd; }
                fwd.Add(edge.ToNodeId);
                if (edge.Bidirectional)
                {
                    if (!adjacency.TryGetValue(edge.ToNodeId, out var rev)) { rev = []; adjacency[edge.ToNodeId] = rev; }
                    rev.Add(edge.FromNodeId);
                }
            }

            int[] rowOrigins = [1, 16, 31, 46, 61, 76]; // first node of rows A-F
            int destination = RedlandsRoadNetwork.EsriHqNodeId;

            foreach (int origin in rowOrigins)
            {
                var visited = new HashSet<int>();
                var queue = new Queue<int>();
                queue.Enqueue(origin);
                visited.Add(origin);

                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    if (cur == destination) break;
                    if (!adjacency.TryGetValue(cur, out var neighbors)) continue;
                    foreach (var nb in neighbors)
                    {
                        if (visited.Add(nb)) queue.Enqueue(nb);
                    }
                }

                Assert.True(visited.Contains(destination),
                    $"Esri HQ is not reachable from row origin node {origin}.");
            }
        }
    }
}
