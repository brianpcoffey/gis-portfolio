namespace Portfolio.Common.DTOs
{
    // DTO for API payloads
    public class GisFeatureDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string FeatureType { get; set; } = "";
        public string Coordinates { get; set; } = "";
        // Expand with more properties for production
    }
}
