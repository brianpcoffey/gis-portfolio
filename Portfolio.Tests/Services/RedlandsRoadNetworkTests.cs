using Portfolio.Common.DTOs;
using Portfolio.Services.Data;

namespace Portfolio.Tests.Services
{
    // Validates the structural integrity and geographic correctness of the
    // real-intersection 155-node Redlands, CA road network (v3 comprehensive).
    public class RedlandsRoadNetworkTests
    {
        // Build once for the whole test class.
        private static readonly RoadGraphDto _graph = RedlandsRoadNetwork.Build();

        // Lazily-built adjacency list used by BFS helpers (respects one-way edges).
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
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(from);
            visited.Add(from);
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

        // ── Node count and ID uniqueness ─────────────────────────────────────────

        [Fact]
        public void Build_ReturnsCorrectNodeCount()
        {
            // 8 Barton + 8 Lugonia + 8 New York St + 8 Colton + 8 University
            // + 5 Highland + 5 Cypress + 1 Esri HQ + 4 I-10 ramps (52-55)
            // + 8 W Redlands Blvd + 6 Stuart Ave + 7 San Bernardino Ave
            // + 5 Base Line Rd + 7 Texas St + 6 Eureka St + 5 California St
            // + 12 I-10 interchanges (100-111) + 6 SR-210 (112-117)
            // + 3 SR-38 (118-120) + 15 downtown detail (121-135)
            // + 2 Highland ext (136-137) + 2 Brockton ext (138-139)
            // + 5 Pioneer Ave (140-144) + 2 Wabash (145-146) + 1 Ford St (147)
            // + 8 Tippecanoe/Mountain View surface (148-155)
            // Total: 155
            Assert.Equal(155, _graph.Nodes.Count);
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
            for (int i = 1; i <= 155; i++)
                Assert.Contains(i, idSet);
        }

        // ── Edge count and basic integrity ───────────────────────────────────────

        [Fact]
        public void Build_EdgeCountIsAboveMinimumExpected()
        {
            // The v3 network has ~275 edges. Assert a conservative lower bound.
            Assert.True(_graph.Edges.Count >= 200,
                $"Expected >=200 edges but got {_graph.Edges.Count}.");
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

        [Fact]
        public void Build_NoDuplicateDirectedEdges()
        {
            // No two edges should share the same (From, To) pair.
            var pairs = _graph.Edges.Select(e => (e.FromNodeId, e.ToNodeId)).ToList();
            Assert.Equal(pairs.Count, pairs.Distinct().Count());
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
        public void Build_EsriHqHasAtLeastTwoConnections()
        {
            int hqId = RedlandsRoadNetwork.EsriHqNodeId;
            int connections = _graph.Edges.Count(
                e => e.FromNodeId == hqId || (e.Bidirectional && e.ToNodeId == hqId));
            Assert.True(connections >= 2,
                $"Esri HQ should have >=2 connections but has {connections}.");
        }

        // ── I-10 ramp nodes (52-55) ──────────────────────────────────────────────

        [Fact]
        public void Build_TippecanoeAndMountainViewRampNodesExist()
        {
            var nodeIds = _graph.Nodes.Select(n => n.Id).ToHashSet();
            foreach (int rampId in new[] { 52, 53, 54, 55 })
                Assert.Contains(rampId, nodeIds);
        }

        [Fact]
        public void Build_TippecanoeAndMountainViewRampNodesAreWestOfMainGrid()
        {
            foreach (int rampId in new[] { 52, 53, 54, 55 })
            {
                var node = _graph.Nodes.Single(n => n.Id == rampId);
                Assert.True(node.Longitude < -117.225,
                    $"Ramp node {rampId} longitude {node.Longitude} should be west of -117.225.");
            }
        }

        // ── I-10 interchange chain (100-111) ─────────────────────────────────────

        [Fact]
        public void Build_I10InterchangeNodesExist()
        {
            var nodeIds = _graph.Nodes.Select(n => n.Id).ToHashSet();
            for (int id = 100; id <= 111; id++)
                Assert.Contains(id, nodeIds);
        }

        [Fact]
        public void Build_I10InterchangeNodesAreNearFreewayLatitude()
        {
            // I-10 through Redlands runs at approximately lat 34.049 ± 0.003.
            for (int id = 100; id <= 111; id++)
            {
                var node = _graph.Nodes.Single(n => n.Id == id);
                Assert.InRange(node.Latitude, 34.045, 34.055);
            }
        }

        // ── SR-210 Foothill Freeway (112-117) ────────────────────────────────────

        [Fact]
        public void Build_SR210NodesExist()
        {
            var nodeIds = _graph.Nodes.Select(n => n.Id).ToHashSet();
            for (int id = 112; id <= 117; id++)
                Assert.Contains(id, nodeIds);
        }

        [Fact]
        public void Build_SR210NodesProgressNortheastward()
        {
            // SR-210 climbs northeast; each successive node should be further north
            // and/or east than the previous.
            var sr210 = Enumerable.Range(112, 6)
                .Select(id => _graph.Nodes.Single(n => n.Id == id))
                .ToList();
            Assert.True(sr210.Last().Latitude > sr210.First().Latitude,
                "SR-210 final node should be north of the first SR-210 node.");
            Assert.True(sr210.Last().Longitude > sr210.First().Longitude,
                "SR-210 final node should be east of the first SR-210 node.");
        }

        // ── SR-38 corridor (118-120) ─────────────────────────────────────────────

        [Fact]
        public void Build_SR38NodesExist()
        {
            var nodeIds = _graph.Nodes.Select(n => n.Id).ToHashSet();
            foreach (int id in new[] { 118, 119, 120 })
                Assert.Contains(id, nodeIds);
        }

        [Fact]
        public void Build_SR38NodesAreBidirectionallyConnected()
        {
            // SR-38 surface nodes 118→119→120 are bidirectional street segments.
            bool has118to119 = _graph.Edges.Any(e =>
                (e.FromNodeId == 118 && e.ToNodeId == 119 && e.Bidirectional) ||
                (e.FromNodeId == 119 && e.ToNodeId == 118 && e.Bidirectional));
            bool has119to120 = _graph.Edges.Any(e =>
                (e.FromNodeId == 119 && e.ToNodeId == 120 && e.Bidirectional) ||
                (e.FromNodeId == 120 && e.ToNodeId == 119 && e.Bidirectional));
            Assert.True(has118to119, "SR-38 nodes 118-119 should be bidirectionally connected.");
            Assert.True(has119to120, "SR-38 nodes 119-120 should be bidirectionally connected.");
        }

        // ── New east-west arterials ───────────────────────────────────────────────

        [Fact]
        public void Build_WRedlandsBvdNodesHaveCorrectLabel()
        {
            // W Redlands Blvd nodes are IDs 56-63.
            var nodes = _graph.Nodes.Where(n => n.Id >= 56 && n.Id <= 63).ToList();
            Assert.Equal(8, nodes.Count);
            Assert.All(nodes, n => Assert.Contains("Redlands Blvd", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_StuartAveNodesHaveCorrectLabel()
        {
            var nodes = _graph.Nodes.Where(n => n.Id >= 64 && n.Id <= 69).ToList();
            Assert.Equal(6, nodes.Count);
            Assert.All(nodes, n => Assert.Contains("Stuart Ave", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_SanBernardinoAveNodesHaveCorrectLabel()
        {
            var nodes = _graph.Nodes.Where(n => n.Id >= 70 && n.Id <= 76).ToList();
            Assert.Equal(7, nodes.Count);
            Assert.All(nodes, n => Assert.Contains("San Bernardino Ave", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_BaseLineRdNodesHaveCorrectLabel()
        {
            var nodes = _graph.Nodes.Where(n => n.Id >= 77 && n.Id <= 81).ToList();
            Assert.Equal(5, nodes.Count);
            Assert.All(nodes, n => Assert.Contains("Base Line Rd", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        // ── New north-south corridors ─────────────────────────────────────────────

        [Fact]
        public void Build_TexasStNodesHaveCorrectLabel()
        {
            // Texas St nodes are IDs 82-88.
            var nodes = _graph.Nodes.Where(n => n.Id >= 82 && n.Id <= 88).ToList();
            Assert.Equal(7, nodes.Count);
            Assert.All(nodes, n => Assert.Contains("Texas St", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_EurekaStNodesHaveCorrectLabel()
        {
            // Eureka St nodes are IDs 89-94.
            var nodes = _graph.Nodes.Where(n => n.Id >= 89 && n.Id <= 94).ToList();
            Assert.Equal(6, nodes.Count);
            Assert.All(nodes, n => Assert.Contains("Eureka St", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_CaliforniaStNodesHaveCorrectLabel()
        {
            // California St nodes are IDs 95-99.
            var nodes = _graph.Nodes.Where(n => n.Id >= 95 && n.Id <= 99).ToList();
            Assert.Equal(5, nodes.Count);
            Assert.All(nodes, n => Assert.Contains("California St", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_PioneerAveNodesHaveCorrectLabel()
        {
            // Pioneer Ave nodes are IDs 140-144.
            var nodes = _graph.Nodes.Where(n => n.Id >= 140 && n.Id <= 144).ToList();
            Assert.Equal(5, nodes.Count);
            Assert.All(nodes, n => Assert.Contains("Pioneer Ave", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        // ── Downtown fine-detail nodes ────────────────────────────────────────────

        [Fact]
        public void Build_CajonStNodesHaveCorrectLabel()
        {
            var nodes = _graph.Nodes.Where(n => n.Id >= 121 && n.Id <= 125).ToList();
            Assert.Equal(5, nodes.Count);
            Assert.All(nodes, n => Assert.Contains("Cajon St", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_OliveAveNodesHaveCorrectLabel()
        {
            var nodes = _graph.Nodes.Where(n => n.Id >= 129 && n.Id <= 131).ToList();
            Assert.Equal(3, nodes.Count);
            Assert.All(nodes, n => Assert.Contains("Olive Ave", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_CenterStNodesHaveCorrectLabel()
        {
            var nodes = _graph.Nodes.Where(n => n.Id >= 132 && n.Id <= 135).ToList();
            Assert.Equal(4, nodes.Count);
            Assert.All(nodes, n => Assert.Contains("Center St", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        // ── Label correctness: old "5th St" / "9th St" names are gone ────────────

        [Fact]
        public void Build_NoNodesLabelledWith5thSt()
        {
            Assert.DoesNotContain(_graph.Nodes, n => n.Label.Contains("5th St", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_NoNodesLabelledWith9thSt()
        {
            Assert.DoesNotContain(_graph.Nodes, n => n.Label.Contains("9th St", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_BrocktonAveExtensionNodeIsNorthOfUniversity()
        {
            // Node 139 is a Brockton Ave mid-segment between University St and Highland Ave.
            var node139 = _graph.Nodes.Single(n => n.Id == 139);
            var universityBrockton = _graph.Nodes.Single(n => n.Id == 40); // University St & Brockton
            Assert.True(node139.Latitude > universityBrockton.Latitude,
                "Brockton Ave extension node 139 should be north of University St & Brockton Ave (node 40).");
        }

        // ── Coordinate sanity ─────────────────────────────────────────────────────

        [Fact]
        public void Build_AllNodeCoordinatesAreFinite()
        {
            foreach (var node in _graph.Nodes)
            {
                Assert.True(double.IsFinite(node.Latitude),  $"Node {node.Id} has non-finite latitude.");
                Assert.True(double.IsFinite(node.Longitude), $"Node {node.Id} has non-finite longitude.");
            }
        }

        [Fact]
        public void Build_AllNodesAreWithinRedlandsBoundingBox()
        {
            // Expanded bounding box: includes Pioneer Ave east, I-10 west ramps,
            // SR-210 northeast corridor, and Highland Ave north extent.
            foreach (var node in _graph.Nodes)
            {
                Assert.InRange(node.Latitude,  34.03, 34.13);
                Assert.InRange(node.Longitude, -117.25, -117.09);
            }
        }

        [Fact]
        public void Build_HighlandAveNodesAreNorthOfCypressAve()
        {
            double highlandLat = _graph.Nodes.Where(n => n.Id >= 41 && n.Id <= 45).Average(n => n.Latitude);
            double cypressLat  = _graph.Nodes.Where(n => n.Id >= 46 && n.Id <= 50).Average(n => n.Latitude);
            Assert.True(highlandLat > cypressLat, "Highland Ave should be north of Cypress Ave.");
        }

        [Fact]
        public void Build_PioneerAveNodesAreEastOfBrocktonAve()
        {
            double pioneerLon  = _graph.Nodes.Where(n => n.Id >= 140 && n.Id <= 144).Average(n => n.Longitude);
            double brocktonLon = _graph.Nodes.Where(n => new[] { 8, 16, 24, 32, 40 }.Contains(n.Id)).Average(n => n.Longitude);
            Assert.True(pioneerLon > brocktonLon, "Pioneer Ave should be east of Brockton Ave.");
        }

        // ── Arterial label sanity (core rows) ────────────────────────────────────

        [Fact]
        public void Build_BartonRdNodesHaveCorrectLabel()
        {
            var nodes = _graph.Nodes.Where(n => n.Id >= 1 && n.Id <= 8).ToList();
            Assert.All(nodes, n => Assert.Contains("Barton Rd", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_LugoniaAveNodesHaveCorrectLabel()
        {
            var nodes = _graph.Nodes.Where(n => n.Id >= 9 && n.Id <= 16).ToList();
            Assert.All(nodes, n => Assert.Contains("Lugonia Ave", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_NewYorkStNodesHaveCorrectLabel()
        {
            var nodes = _graph.Nodes.Where(n => n.Id >= 17 && n.Id <= 24).ToList();
            Assert.All(nodes, n => Assert.Contains("New York St", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_ColtonAveNodesHaveCorrectLabel()
        {
            var nodes = _graph.Nodes.Where(n => n.Id >= 25 && n.Id <= 32).ToList();
            Assert.All(nodes, n => Assert.Contains("Colton Ave", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_UniversityStNodesHaveCorrectLabel()
        {
            var nodes = _graph.Nodes.Where(n => n.Id >= 33 && n.Id <= 40).ToList();
            Assert.All(nodes, n => Assert.Contains("University St", n.Label, StringComparison.OrdinalIgnoreCase));
        }

        // ── Graph name ────────────────────────────────────────────────────────────

        [Fact]
        public void Build_GraphNameContainsNodeCountAndCity()
        {
            Assert.Contains("155", _graph.GraphName, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Redlands", _graph.GraphName, StringComparison.OrdinalIgnoreCase);
        }

        // ── Reachability: BFS from multiple origin classes to Esri HQ ────────────

        [Fact]
        public void Build_EsriHqIsReachableFromAllCoreArterialOrigins()
        {
            // First node on each original E-W arterial row.
            int[] origins = [1, 9, 17, 25, 33, 41];
            int destination = RedlandsRoadNetwork.EsriHqNodeId;
            foreach (int origin in origins)
                Assert.True(CanReach(origin, destination),
                    $"Esri HQ is not reachable from core arterial origin node {origin}.");
        }

        [Fact]
        public void Build_EsriHqIsReachableFromNewArterialOrigins()
        {
            // First node on each new E-W arterial added in the 155-node edition.
            int[] origins = [56, 64, 70, 77, 132, 121];
            int destination = RedlandsRoadNetwork.EsriHqNodeId;
            foreach (int origin in origins)
                Assert.True(CanReach(origin, destination),
                    $"Esri HQ is not reachable from new arterial origin node {origin}.");
        }

        [Fact]
        public void Build_EsriHqIsReachableFromFreewayRamps()
        {
            // I-10 ramps at Tippecanoe (52) and Mountain View (54/55) should eventually
            // connect to the surface grid and thus to Esri HQ.
            int destination = RedlandsRoadNetwork.EsriHqNodeId;
            foreach (int ramp in new[] { 54, 55 })
                Assert.True(CanReach(ramp, destination),
                    $"Esri HQ is not reachable from I-10 ramp node {ramp}.");
        }

        [Fact]
        public void Build_EsriHqIsReachableFromI10InterchangeNodes()
        {
            int destination = RedlandsRoadNetwork.EsriHqNodeId;
            // WB off-ramp nodes drop to the surface grid; check a representative set.
            foreach (int node in new[] { 100, 102, 106, 108 })
                Assert.True(CanReach(node, destination),
                    $"Esri HQ is not reachable from I-10 interchange node {node}.");
        }

        [Fact]
        public void Build_EsriHqIsReachableFromPioneerAve()
        {
            int destination = RedlandsRoadNetwork.EsriHqNodeId;
            Assert.True(CanReach(140, destination), "Esri HQ is not reachable from Pioneer Ave (node 140).");
            Assert.True(CanReach(142, destination), "Esri HQ is not reachable from New York St & Pioneer Ave (node 142).");
        }

        [Fact]
        public void Build_EsriHqIsReachableFromWestApproachSurface()
        {
            // Tippecanoe/Mountain View surface nodes (148-155).
            int destination = RedlandsRoadNetwork.EsriHqNodeId;
            foreach (int node in new[] { 148, 149, 150, 151, 152, 153, 154, 155 })
                Assert.True(CanReach(node, destination),
                    $"Esri HQ is not reachable from west-approach surface node {node}.");
        }
    }
}
