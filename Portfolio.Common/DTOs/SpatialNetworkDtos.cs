namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// A node (vertex) in a routing graph, positioned by geographic coordinate.
    /// </summary>
    public class GraphNodeDto
    {
        /// <summary>Unique identifier of the node within the graph.</summary>
        public int Id { get; set; }

        /// <summary>WGS84 latitude of the node.</summary>
        public double Latitude { get; set; }

        /// <summary>WGS84 longitude of the node.</summary>
        public double Longitude { get; set; }

        /// <summary>Optional human-readable label for the node.</summary>
        public string? Label { get; set; }
    }

    /// <summary>
    /// An edge (link) connecting two nodes in a routing graph, with a traversal cost.
    /// </summary>
    public class GraphEdgeDto
    {
        /// <summary>Id of the node the edge starts at.</summary>
        public int FromNodeId { get; set; }

        /// <summary>Id of the node the edge ends at.</summary>
        public int ToNodeId { get; set; }

        /// <summary>Cost (e.g. distance or travel time) of traversing the edge.</summary>
        public double Cost { get; set; }

        /// <summary>True if the edge may be traversed in both directions (default true).</summary>
        public bool Bidirectional { get; set; } = true;
    }

    /// <summary>
    /// Request to find the least-cost route between two nodes of a supplied graph.
    /// </summary>
    public class RouteRequestDto
    {
        /// <summary>Nodes that make up the graph.</summary>
        public List<GraphNodeDto> Nodes { get; set; } = [];

        /// <summary>Edges connecting the graph's nodes.</summary>
        public List<GraphEdgeDto> Edges { get; set; } = [];

        /// <summary>Id of the node the route starts from.</summary>
        public int StartNodeId { get; set; }

        /// <summary>Id of the node the route ends at.</summary>
        public int EndNodeId { get; set; }

        /// <summary>"dijkstra" (default) or "astar"</summary>
        public string Algorithm { get; set; } = "dijkstra";
    }

    /// <summary>
    /// Result of a routing request: the computed path, its cost, and search diagnostics.
    /// </summary>
    public class RouteResultDto
    {
        /// <summary>True when the route was computed by the native-accelerated engine.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>True if a route between the start and end nodes was found.</summary>
        public bool Found { get; set; }

        /// <summary>Total cost of the route (sum of traversed edge costs).</summary>
        public double TotalCost { get; set; }

        /// <summary>Ids of the nodes along the route, in order from start to end.</summary>
        public List<int> NodeIds { get; set; } = [];

        /// <summary>Coordinates of the route path, in order from start to end.</summary>
        public List<CoordinateDto> Path { get; set; } = [];

        /// <summary>Total number of nodes the search explored before completing.</summary>
        public int ExploredNodes { get; set; }

        /// <summary>Ids of every node the search settled, in exploration order. Drives
        /// the "search space" map overlay that contrasts A* against Dijkstra. May be
        /// empty when the native engine computes the path.</summary>
        public List<int> ExploredNodeIds { get; set; } = [];

        /// <summary>Total length of the route, in kilometers.</summary>
        public double DistanceKm { get; set; }

        /// <summary>Estimated travel time along the route, in minutes.</summary>
        public double EstimatedMinutes { get; set; }

        /// <summary>Name of the algorithm that produced the route ("dijkstra" or "astar").</summary>
        public string AlgorithmUsed { get; set; } = "dijkstra";
    }

    /// <summary>
    /// Request to compute the set of nodes reachable from an origin within a cost budget.
    /// </summary>
    public class ServiceAreaRequestDto
    {
        /// <summary>Nodes that make up the graph.</summary>
        public List<GraphNodeDto> Nodes { get; set; } = [];

        /// <summary>Edges connecting the graph's nodes.</summary>
        public List<GraphEdgeDto> Edges { get; set; } = [];

        /// <summary>Id of the node the service area is measured from.</summary>
        public int OriginNodeId { get; set; }

        /// <summary>Maximum cumulative cost a node may be from the origin to be included.</summary>
        public double MaxCost { get; set; }
    }

    /// <summary>
    /// Result of a service-area computation: the nodes reachable within the cost budget.
    /// </summary>
    public class ServiceAreaResultDto
    {
        /// <summary>True when the computation was performed by the native-accelerated engine.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>Ids of the nodes reachable from the origin within the cost budget.</summary>
        public List<int> ReachableNodeIds { get; set; } = [];
    }

    /// <summary>
    /// A named road graph (nodes and edges) with a designated destination node.
    /// </summary>
    public class RoadGraphDto
    {
        /// <summary>Nodes that make up the road graph.</summary>
        public List<GraphNodeDto> Nodes { get; set; } = [];

        /// <summary>Edges connecting the road graph's nodes.</summary>
        public List<GraphEdgeDto> Edges { get; set; } = [];

        /// <summary>Id of the graph's default destination node.</summary>
        public int DestinationNodeId { get; set; }

        /// <summary>Human-readable name of the road graph.</summary>
        public string GraphName { get; set; } = string.Empty;
    }
}
