namespace Portfolio.Common.Models
{
    // GIS Feature model for EF Core
    public class GisFeature
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string FeatureType { get; set; } = ""; // "Marker" or "Polygon"
        public string Coordinates { get; set; } = ""; // GeoJSON or custom format
        public string CreatedBy { get; set; } = "";
        // Expand with more properties for production
    }
}
