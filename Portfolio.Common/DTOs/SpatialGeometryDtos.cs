namespace Portfolio.Common.DTOs
{
    public class CoordinateDto
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class GeometryPointSetDto
    {
        public List<CoordinateDto> Points { get; set; } = [];
    }

    public class TriangulationResultDto
    {
        public bool NativeAccelerated { get; set; }
        public List<TriangleDto> Triangles { get; set; } = [];
    }

    public class TriangleDto
    {
        public CoordinateDto A { get; set; } = new();
        public CoordinateDto B { get; set; } = new();
        public CoordinateDto C { get; set; } = new();
    }

    public class PolygonClipRequestDto
    {
        public List<CoordinateDto> Subject { get; set; } = [];
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
    }

    public class PolygonOperationResultDto
    {
        public bool NativeAccelerated { get; set; }
        public List<CoordinateDto> Vertices { get; set; } = [];
    }
}
