namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// Request to compute a hillshade raster from an elevation grid.
    /// </summary>
    public class RasterHillshadeRequestDto
    {
        /// <summary>Width of the elevation grid, in cells.</summary>
        public int Width { get; set; }

        /// <summary>Height of the elevation grid, in cells.</summary>
        public int Height { get; set; }

        /// <summary>Ground distance represented by each cell, in meters.</summary>
        public double CellSize { get; set; } = 30;

        /// <summary>Light-source azimuth in degrees clockwise from north (default 315).</summary>
        public double AzimuthDegrees { get; set; } = 315;

        /// <summary>Light-source altitude angle above the horizon, in degrees (default 45).</summary>
        public double AltitudeDegrees { get; set; } = 45;

        /// <summary>Elevation values in row-major order (Width * Height entries).</summary>
        public List<double> Elevation { get; set; } = [];
    }

    /// <summary>
    /// Result of a hillshade computation: per-cell shading intensities.
    /// </summary>
    public class RasterHillshadeResultDto
    {
        /// <summary>Width of the output raster, in cells.</summary>
        public int Width { get; set; }

        /// <summary>Height of the output raster, in cells.</summary>
        public int Height { get; set; }

        /// <summary>True when the computation was performed by the native-accelerated engine.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>Shading intensities (0-255) in row-major order.</summary>
        public List<byte> Intensities { get; set; } = [];
    }

    /// <summary>
    /// Request to render a heatmap raster from weighted points over a bounding box.
    /// </summary>
    public class HeatmapRequestDto
    {
        /// <summary>Width of the output heatmap, in cells.</summary>
        public int Width { get; set; }

        /// <summary>Height of the output heatmap, in cells.</summary>
        public int Height { get; set; }

        /// <summary>Minimum X (left edge) of the area to render.</summary>
        public double MinX { get; set; }

        /// <summary>Minimum Y (bottom edge) of the area to render.</summary>
        public double MinY { get; set; }

        /// <summary>Maximum X (right edge) of the area to render.</summary>
        public double MaxX { get; set; }

        /// <summary>Maximum Y (top edge) of the area to render.</summary>
        public double MaxY { get; set; }

        /// <summary>Influence radius of each point, in coordinate units (default 0.05).</summary>
        public double Radius { get; set; } = 0.05;

        /// <summary>Weighted points contributing heat to the map.</summary>
        public List<WeightedPointDto> Points { get; set; } = [];
    }

    /// <summary>
    /// A point with an associated weight used as a heatmap input.
    /// </summary>
    public class WeightedPointDto
    {
        /// <summary>X coordinate of the point.</summary>
        public double X { get; set; }

        /// <summary>Y coordinate of the point.</summary>
        public double Y { get; set; }

        /// <summary>Relative weight (intensity) of the point (default 1).</summary>
        public double Weight { get; set; } = 1;
    }

    /// <summary>
    /// Result of a heatmap computation: per-cell density values.
    /// </summary>
    public class HeatmapResultDto
    {
        /// <summary>Width of the output heatmap, in cells.</summary>
        public int Width { get; set; }

        /// <summary>Height of the output heatmap, in cells.</summary>
        public int Height { get; set; }

        /// <summary>True when the computation was performed by the native-accelerated engine.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>Density values in row-major order (Width * Height entries).</summary>
        public List<double> Values { get; set; } = [];
    }
}
