namespace Portfolio.Common.DTOs
{
    public class GraphNodeDto
    {
        public int Id { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Label { get; set; }
    }

    public class GraphEdgeDto
    {
        public int FromNodeId { get; set; }
        public int ToNodeId { get; set; }
        public double Cost { get; set; }
        public bool Bidirectional { get; set; } = true;
    }

    public class RouteRequestDto
    {
        public List<GraphNodeDto> Nodes { get; set; } = [];
        public List<GraphEdgeDto> Edges { get; set; } = [];
        public int StartNodeId { get; set; }
        public int EndNodeId { get; set; }
        /// <summary>"dijkstra" (default) or "astar"</summary>
        public string Algorithm { get; set; } = "dijkstra";
    }

    public class RouteResultDto
    {
        public bool NativeAccelerated { get; set; }
        public bool Found { get; set; }
        public double TotalCost { get; set; }
        public List<int> NodeIds { get; set; } = [];
        public List<CoordinateDto> Path { get; set; } = [];
        public int ExploredNodes { get; set; }
        public double DistanceKm { get; set; }
        public double EstimatedMinutes { get; set; }
        public string AlgorithmUsed { get; set; } = "dijkstra";
    }

    public class ServiceAreaRequestDto
    {
        public List<GraphNodeDto> Nodes { get; set; } = [];
        public List<GraphEdgeDto> Edges { get; set; } = [];
        public int OriginNodeId { get; set; }
        public double MaxCost { get; set; }
    }

    public class ServiceAreaResultDto
    {
        public bool NativeAccelerated { get; set; }
        public List<int> ReachableNodeIds { get; set; } = [];
    }

    public class RoadGraphDto
    {
        public List<GraphNodeDto> Nodes { get; set; } = [];
        public List<GraphEdgeDto> Edges { get; set; } = [];
        public int DestinationNodeId { get; set; }
        public string GraphName { get; set; } = string.Empty;
    }
}
