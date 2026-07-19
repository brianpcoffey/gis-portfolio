namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// A two-dimensional coordinate (X/Y) in the working coordinate system.
    /// </summary>
    public class CoordinateDto
    {
        /// <summary>X coordinate (e.g. longitude or easting).</summary>
        public double X { get; set; }

        /// <summary>Y coordinate (e.g. latitude or northing).</summary>
        public double Y { get; set; }
    }

    /// <summary>
    /// An ordered set of coordinates, e.g. input points for a geometry operation.
    /// </summary>
    public class GeometryPointSetDto
    {
        /// <summary>The coordinates in the set.</summary>
        public List<CoordinateDto> Points { get; set; } = [];
    }

    /// <summary>
    /// Result of triangulating a point set into a mesh of triangles.
    /// </summary>
    public class TriangulationResultDto
    {
        /// <summary>True when the computation was performed by the native-accelerated engine.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>The triangles that make up the resulting mesh.</summary>
        public List<TriangleDto> Triangles { get; set; } = [];
    }

    /// <summary>
    /// A single triangle defined by its three corner coordinates.
    /// </summary>
    public class TriangleDto
    {
        /// <summary>First corner of the triangle.</summary>
        public CoordinateDto A { get; set; } = new();

        /// <summary>Second corner of the triangle.</summary>
        public CoordinateDto B { get; set; } = new();

        /// <summary>Third corner of the triangle.</summary>
        public CoordinateDto C { get; set; } = new();
    }

    /// <summary>
    /// Request to clip a subject polygon against a rectangular bounding box.
    /// </summary>
    public class PolygonClipRequestDto
    {
        /// <summary>Vertices of the subject polygon to clip.</summary>
        public List<CoordinateDto> Subject { get; set; } = [];

        /// <summary>Minimum X (left edge) of the clip rectangle.</summary>
        public double MinX { get; set; }

        /// <summary>Minimum Y (bottom edge) of the clip rectangle.</summary>
        public double MinY { get; set; }

        /// <summary>Maximum X (right edge) of the clip rectangle.</summary>
        public double MaxX { get; set; }

        /// <summary>Maximum Y (top edge) of the clip rectangle.</summary>
        public double MaxY { get; set; }
    }

    /// <summary>
    /// Result of a polygon operation, returning the resulting polygon's vertices.
    /// </summary>
    public class PolygonOperationResultDto
    {
        /// <summary>True when the computation was performed by the native-accelerated engine.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>Vertices of the resulting polygon, in order.</summary>
        public List<CoordinateDto> Vertices { get; set; } = [];
    }
}
