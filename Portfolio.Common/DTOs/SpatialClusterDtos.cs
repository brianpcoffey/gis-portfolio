namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// A two-dimensional point submitted for density-based clustering.
    /// </summary>
    public class ClusterPointDto
    {
        /// <summary>X coordinate (longitude in the demo dataset).</summary>
        public double X { get; set; }

        /// <summary>Y coordinate (latitude in the demo dataset).</summary>
        public double Y { get; set; }
    }

    /// <summary>
    /// Request to cluster a set of points with DBSCAN.
    /// </summary>
    public class DbscanRequestDto
    {
        /// <summary>Neighborhood radius, in coordinate units (default 0.08).</summary>
        public double Epsilon { get; set; } = 0.08;

        /// <summary>Minimum neighbors (including the point itself) for a core point (default 4).</summary>
        public int MinPoints { get; set; } = 4;

        /// <summary>Points to cluster.</summary>
        public List<ClusterPointDto> Points { get; set; } = [];
    }

    /// <summary>
    /// A point after clustering, carrying its assigned cluster label.
    /// </summary>
    public class ClusteredPointDto
    {
        /// <summary>X coordinate of the point.</summary>
        public double X { get; set; }

        /// <summary>Y coordinate of the point.</summary>
        public double Y { get; set; }

        /// <summary>Zero-based cluster id, or -1 when the point is classified as noise.</summary>
        public int ClusterId { get; set; }
    }

    /// <summary>
    /// Result of a DBSCAN run: labelled points plus cluster and noise summaries.
    /// </summary>
    public class DbscanResultDto
    {
        /// <summary>True when the computation was performed by the native-accelerated engine.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>Number of distinct clusters discovered.</summary>
        public int ClusterCount { get; set; }

        /// <summary>Number of points classified as noise (not part of any cluster).</summary>
        public int NoiseCount { get; set; }

        /// <summary>Per-cluster point counts, indexed by cluster id.</summary>
        public List<int> ClusterSizes { get; set; } = [];

        /// <summary>Points in input order, each carrying its cluster label.</summary>
        public List<ClusteredPointDto> Points { get; set; } = [];
    }
}
