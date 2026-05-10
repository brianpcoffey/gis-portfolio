using Portfolio.Common.DTOs;
using Portfolio.Services.Data;

namespace Portfolio.Tests.Services
{
    // Validates the structural integrity and geographic correctness of the
    // real-intersection 55-node Redlands, CA road network.
    public class RedlandsRoadNetworkTests
    {
        // Build once for the whole test class.
        private static readonly RoadGraphDto _graph = RedlandsRoadNetwork.Build();

        // Node count and ID uniqueness

        [Fact]
        public void Build_ReturnsCorrectNodeCount()
        {
            // 8 Barton Rd + 8 Lugonia + 8 New York St + 8 Colton + 8 University
            // + 5 Highland + 5 Cypress + 1 Esri HQ + 4 I-10 ramps = 55
            Assert.Equal(55, _graph.Nodes.Count);
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
            var idSet = _graph.Nodes.Select(n => n.Id).ToHashSet();
            for (int i = 1; i <= 55; i++)
                Assert.Contains(i, idSet);
        }

        // Edge count and minimum connectivity

        [Fact]
        public void Build_EdgeCountIsAboveMinimumExpected()
        {
            // 7 Barton + 7 Lugonia + 9 NewYorkSt (incl HQ) + 7 Colton + 7 University
            // + 4 Highland + 4 Cypress (EW) = 45 EW
            // + 4 Orange + 6 Church + 6 State + 6 Fifth + 6 Ninth + 6 Brookside
            // + 4 Tennessee + 4 Brockton (NS) = 42 NS
            // + 7 I-10 ramp edges + 1 HQ shortcut = 8 specials
            // Total minimum: 95
            Assert.True(_graph.Edges.Count >= 90,
                $"Expected >=90 edges but got {_graph.Edges.Count}.");
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
                    $"Edge {edge.FromNodeId}>{edge.ToNodeId} has invalid cost {edge.Cost}.");
            }
        }

        [Fact]
        public void Build_NoSelfLoops()
        {
            foreach (var edge in _graph.Edges)
                Assert.NotEqual(edge.FromNodeId, edge.ToNodeId);
        }

        // Special node identity

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
        public void Build_EsriHqHasAtLeastThreeConnections()
        {
            int hqId = RedlandsRoadNetwork.EsriHqNodeId;
            int connections = _graph.Edges.Count(
                e => e.FromNodeId == hqId || (e.Bidirectional && e.ToNodeId == hqId));
            Assert.True(connections >= 3,
                $"Esri HQ should have >=3 connections but has {connections}.");
        }

        // I-10 ramp nodes

        [Fact]
        public void Build_FreewayRampNodesExist()
        {
            var nodeIds = _graph.Nodes.Select(n => n.Id).ToHashSet();
            foreach (int rampId in new[] { 52, 53, 54, 55 })
                Assert.Contains(rampId, nodeIds);
        }

        [Fact]
        public void Build_FreewayRampNodesAreWestOfMainGrid()
        {
            // All four ramp nodes sit west of the westernmost main-grid longitude (-117.222).
            foreach (int rampId in new[] { 52, 53, 54, 55 })
            {
                var node = _graph.Nodes.Single(n => n.Id == rampId);
                Assert.True(node.Longitude < -117.222,
                    $"Ramp node {rampId} longitude {node.Longitude} should be west of -117.222.");
            }
        }

        // Coordinate sanity

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
            // Generous bounding box covering Redlands + Highland + I-10 ramps.
            foreach (var node in _graph.Nodes)
            {
                Assert.InRange(node.Latitude,  34.03, 34.13);
                Assert.InRange(node.Longitude, -117.25, -117.10);
            }
        }

        // Arterial label sanity

        [Fact]
        public void Build_BartonRdNodesHaveCorrectLabel()
        {
            // Barton Rd nodes are IDs 1-8.
            var bartonNodes = _graph.Nodes.Where(n => n.Id >= 1 && n.Id <= 8).ToList();
            Assert.All(bartonNodes, n => Assert.Contains("Barton Rd", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_NewYorkStNodesHaveCorrectLabel()
        {
            // New York St nodes are IDs 17-24.
            var nyNodes = _graph.Nodes.Where(n => n.Id >= 17 && n.Id <= 24).ToList();
            Assert.All(nyNodes, n => Assert.Contains("New York St", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_GraphNameContainsNodeCountAndCity()
        {
            Assert.Contains("55", _graph.GraphName, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Redlands", _graph.GraphName, StringComparison.OrdinalIgnoreCase);
        }

        // Reachability smoke test via BFS

        [Fact]
        public void Build_EsriHqIsReachableFromAllArterialOrigins()
        {
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

            // First node on each arterial row: Barton(1), Lugonia(9), NewYorkSt(17),
            // Colton(25), University(33), Highland(41)
            int[] rowOrigins = [1, 9, 17, 25, 33, 41];
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
                    $"Esri HQ is not reachable from arterial origin node {origin}.");
            }
        }
    }
}
