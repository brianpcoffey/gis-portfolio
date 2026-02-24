namespace Portfolio.Common.DTOs
{
    public class SavedFeatureCreateDto
    {
        public string LayerId { get; set; } = string.Empty;
        public string FeatureId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string GeometryJson { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}