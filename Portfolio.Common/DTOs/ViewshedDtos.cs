namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// Request to compute a line-of-sight viewshed over an elevation grid.
    /// </summary>
    public class ViewshedRequestDto
    {
        /// <summary>Width of the elevation grid, in cells.</summary>
        public int Width { get; set; }

        /// <summary>Height of the elevation grid, in cells.</summary>
        public int Height { get; set; }

        /// <summary>Ground distance represented by each cell, in meters (default 30).</summary>
        public double CellSize { get; set; } = 30;

        /// <summary>Observer column index (0-based).</summary>
        public int ObserverX { get; set; }

        /// <summary>Observer row index (0-based).</summary>
        public int ObserverY { get; set; }

        /// <summary>Observer eye height above the terrain surface, in meters (default 2).</summary>
        public double ObserverHeight { get; set; } = 2;

        /// <summary>Elevation values in row-major order (Width * Height entries).</summary>
        public List<double> Elevation { get; set; } = [];
    }

    /// <summary>
    /// Result of a viewshed computation: per-cell visibility from the observer.
    /// </summary>
    public class ViewshedResultDto
    {
        /// <summary>Width of the output grid, in cells.</summary>
        public int Width { get; set; }

        /// <summary>Height of the output grid, in cells.</summary>
        public int Height { get; set; }

        /// <summary>Observer column index echoed back for rendering.</summary>
        public int ObserverX { get; set; }

        /// <summary>Observer row index echoed back for rendering.</summary>
        public int ObserverY { get; set; }

        /// <summary>Number of cells visible from the observer.</summary>
        public int VisibleCells { get; set; }

        /// <summary>Total number of cells in the grid.</summary>
        public int TotalCells { get; set; }

        /// <summary>True when the computation was performed by the native-accelerated engine.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>Visibility flags (1 = visible, 0 = hidden) in row-major order.</summary>
        public List<byte> Visibility { get; set; } = [];
    }
}
