namespace Portfolio.Common.DTOs
{
    public class SavedFeatureDto
    {
        public int Id { get; set; }
        public string LayerId { get; set; } = string.Empty;
        public string FeatureId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string GeometryJson { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime DateSaved { get; set; }
        public DateTime? LastModified { get; set; }
    }
}