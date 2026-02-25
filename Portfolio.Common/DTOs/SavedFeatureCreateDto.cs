namespace Portfolio.Common.DTOs
{
    public class CreateSavedFeatureDto
    {
        public string LayerId { get; set; } = string.Empty;
        public string FeatureId { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? GeometryJson { get; set; }
        public string? Description { get; set; }
        public int? CollectionId { get; set; }
    }
}