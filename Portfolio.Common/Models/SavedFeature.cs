namespace Portfolio.Common.Models
{
    public class SavedFeature
    {
        public int Id { get; set; }
        public string LayerId { get; set; } = string.Empty;
        public string FeatureId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string GeometryJson { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? CollectionId { get; set; }
        public Collection? Collection { get; set; }
        public DateTime DateSaved { get; set; }
        public DateTime? LastModified { get; set; }
        public ICollection<UserNote> UserNotes { get; set; } = new List<UserNote>();
    }
}