namespace Portfolio.Common.DTOs
{
    public class RasterHillshadeRequestDto
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public double CellSize { get; set; } = 30;
        public double AzimuthDegrees { get; set; } = 315;
        public double AltitudeDegrees { get; set; } = 45;
        public List<double> Elevation { get; set; } = [];
    }

    public class RasterHillshadeResultDto
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public bool NativeAccelerated { get; set; }
        public List<byte> Intensities { get; set; } = [];
    }

    public class HeatmapRequestDto
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double Radius { get; set; } = 0.05;
        public List<WeightedPointDto> Points { get; set; } = [];
    }

    public class WeightedPointDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Weight { get; set; } = 1;
    }

    public class HeatmapResultDto
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public bool NativeAccelerated { get; set; }
        public List<double> Values { get; set; } = [];
    }
}
