namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// A point submitted for a spatial join against a set of zones.
    /// </summary>
    public class OverlayPointDto
    {
        /// <summary>X coordinate of the point.</summary>
        public double X { get; set; }

        /// <summary>Y coordinate of the point.</summary>
        public double Y { get; set; }
    }

    /// <summary>
    /// A named polygon zone defined by an ordered ring of vertices.
    /// </summary>
    public class OverlayZoneDto
    {
        /// <summary>Human-readable zone name (echoed back on the summary).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Ordered ring vertices. The ring is closed implicitly (last connects to first).</summary>
        public List<OverlayPointDto> Ring { get; set; } = [];
    }

    /// <summary>
    /// Request to assign each point to the first zone that contains it (point-in-polygon spatial join).
    /// </summary>
    public class SpatialJoinRequestDto
    {
        /// <summary>Points to tag with their containing zone.</summary>
        public List<OverlayPointDto> Points { get; set; } = [];

        /// <summary>Candidate zones, tested in order; the first match wins.</summary>
        public List<OverlayZoneDto> Zones { get; set; } = [];
    }

    /// <summary>
    /// A point after the spatial join, carrying its assigned zone.
    /// </summary>
    public class TaggedPointDto
    {
        /// <summary>X coordinate of the point.</summary>
        public double X { get; set; }

        /// <summary>Y coordinate of the point.</summary>
        public double Y { get; set; }

        /// <summary>Zero-based zone index, or -1 when the point falls outside every zone.</summary>
        public int ZoneIndex { get; set; }
    }

    /// <summary>
    /// A per-zone rollup produced by the spatial join.
    /// </summary>
    public class ZoneSummaryDto
    {
        /// <summary>Zero-based zone index.</summary>
        public int ZoneIndex { get; set; }

        /// <summary>Zone name echoed from the request.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Number of points contained by this zone.</summary>
        public int PointCount { get; set; }
    }

    /// <summary>
    /// Result of a spatial join: tagged points, per-zone counts, and an unassigned tally.
    /// </summary>
    public class SpatialJoinResultDto
    {
        /// <summary>True when the computation was performed by the native-accelerated engine.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>Number of points that fell inside at least one zone.</summary>
        public int AssignedCount { get; set; }

        /// <summary>Number of points that fell outside every zone.</summary>
        public int UnassignedCount { get; set; }

        /// <summary>Per-zone point-count rollups.</summary>
        public List<ZoneSummaryDto> Zones { get; set; } = [];

        /// <summary>Points in input order, each carrying its assigned zone index.</summary>
        public List<TaggedPointDto> Points { get; set; } = [];
    }
}
