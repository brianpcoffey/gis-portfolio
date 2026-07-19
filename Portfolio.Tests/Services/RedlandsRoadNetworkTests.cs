using Portfolio.Common.DTOs;
using Portfolio.Services.Data;

namespace Portfolio.Tests.Services
{
    // Validates the structural integrity and geographic sanity of the Redlands, CA
    // road network. The network is generated from real OpenStreetMap data: every node
    // is an actual street intersection with true WGS84 coordinates, and every edge is a
    // real road segment. These tests therefore assert data-agnostic invariants (unique
    // ids, valid references, positive costs, reachability, Redlands bounding box) rather
    // than hard-coded coordinates, so the suite stays valid when the OSM extract is
    // regenerated.
    public class RedlandsRoadNetworkTests
    {
        private static readonly RoadGraphDto _graph = RedlandsRoadNetwork.Build();
        private static readonly Dictionary<int, List<int>> _adjacency = BuildAdjacency(_graph);

        private static Dictionary<int, List<int>> BuildAdjacency(RoadGraphDto g)
        {
            var adj = new Dictionary<int, List<int>>();
            foreach (var edge in g.Edges)
            {
                if (!adj.TryGetValue(edge.FromNodeId, out var fwd)) { fwd = []; adj[edge.FromNodeId] = fwd; }
                fwd.Add(edge.ToNodeId);
                if (edge.Bidirectional)
                {
                    if (!adj.TryGetValue(edge.ToNodeId, out var rev)) { rev = []; adj[edge.ToNodeId] = rev; }
                    rev.Add(edge.FromNodeId);
                }
            }
            return adj;
        }

        private static bool CanReach(int from, int to)
        {
            var visited = new HashSet<int> { from };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (cur == to) return true;
                if (!_adjacency.TryGetValue(cur, out var neighbors)) continue;
                foreach (var nb in neighbors)
                    if (visited.Add(nb)) queue.Enqueue(nb);
            }
            return false;
        }

        // ── Basic structure ──────────────────────────────────────────────────────

        [Fact]
        public void Build_ReturnsNonTrivialNetwork()
        {
            Assert.True(_graph.Nodes.Count >= 100, $"Expected >=100 nodes but got {_graph.Nodes.Count}.");
            Assert.True(_graph.Edges.Count >= 150, $"Expected >=150 edges but got {_graph.Edges.Count}.");
        }

        [Fact]
        public void Build_AllNodeIdsAreUnique()
        {
            var ids = _graph.Nodes.Select(n => n.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
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
                Assert.True(edge.Cost > 0 && double.IsFinite(edge.Cost),
                    $"Edge {edge.FromNodeId}>{edge.ToNodeId} has invalid cost {edge.Cost}.");
        }

        [Fact]
        public void Build_NoSelfLoops()
        {
            foreach (var edge in _graph.Edges)
                Assert.NotEqual(edge.FromNodeId, edge.ToNodeId);
        }

        [Fact]
        public void Build_NoDuplicateDirectedEdges()
        {
            var pairs = _graph.Edges.Select(e => (e.FromNodeId, e.ToNodeId)).ToList();
            Assert.Equal(pairs.Count, pairs.Distinct().Count());
        }

        [Fact]
        public void Build_EveryNodeHasAtLeastOneEdge()
        {
            var referenced = new HashSet<int>();
            foreach (var e in _graph.Edges) { referenced.Add(e.FromNodeId); referenced.Add(e.ToNodeId); }
            foreach (var n in _graph.Nodes)
                Assert.Contains(n.Id, referenced);
        }

        [Fact]
        public void Build_EveryNodeHasANonEmptyLabel()
        {
            foreach (var n in _graph.Nodes)
                Assert.False(string.IsNullOrWhiteSpace(n.Label), $"Node {n.Id} has an empty label.");
        }

        // ── Coordinate sanity ─────────────────────────────────────────────────────

        [Fact]
        public void Build_AllNodeCoordinatesAreFinite()
        {
            foreach (var node in _graph.Nodes)
            {
                Assert.True(double.IsFinite(node.Latitude), $"Node {node.Id} has non-finite latitude.");
                Assert.True(double.IsFinite(node.Longitude), $"Node {node.Id} has non-finite longitude.");
            }
        }

        [Fact]
        public void Build_AllNodesAreWithinRedlandsBoundingBox()
        {
            foreach (var node in _graph.Nodes)
            {
                Assert.InRange(node.Latitude, 34.03, 34.10);
                Assert.InRange(node.Longitude, -117.23, -117.11);
            }
        }

        // ── Esri HQ destination ───────────────────────────────────────────────────

        [Fact]
        public void Build_DestinationNodeIdIsEsriHq()
        {
            Assert.Equal(RedlandsRoadNetwork.EsriHqNodeId, _graph.DestinationNodeId);
        }

        [Fact]
        public void Build_EsriHqNodeExistsAtRealCampusCoordinate()
        {
            var hq = _graph.Nodes.Single(n => n.Id == RedlandsRoadNetwork.EsriHqNodeId);
            // Esri HQ, 380 New York St, Redlands (OSM: 34.0573, -117.1941).
            Assert.InRange(hq.Latitude, 34.056, 34.059);
            Assert.InRange(hq.Longitude, -117.196, -117.192);
            Assert.Contains("Esri HQ", hq.Label, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Build_EsriHqHasAtLeastTwoConnections()
        {
            int hqId = RedlandsRoadNetwork.EsriHqNodeId;
            int connections = _graph.Edges.Count(
                e => e.FromNodeId == hqId || (e.Bidirectional && e.ToNodeId == hqId));
            Assert.True(connections >= 2, $"Esri HQ should have >=2 connections but has {connections}.");
        }

        // ── Reachability: every intersection can route to Esri HQ ─────────────────

        [Fact]
        public void Build_EsriHqIsReachableFromEveryNode()
        {
            int destination = RedlandsRoadNetwork.EsriHqNodeId;
            foreach (var node in _graph.Nodes)
                Assert.True(CanReach(node.Id, destination),
                    $"Esri HQ is not reachable from node {node.Id} ({node.Label}).");
        }

        // ── Graph name ────────────────────────────────────────────────────────────

        [Fact]
        public void Build_GraphNameContainsNodeCountAndCity()
        {
            Assert.Contains(_graph.Nodes.Count.ToString(), _graph.GraphName);
            Assert.Contains("Redlands", _graph.GraphName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
