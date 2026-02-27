namespace Portfolio.Common.Models
{
    public class Collection
    {
        public int Id { get; set; }
        public string OwnerId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Color { get; set; } = "#6c757d";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModified { get; set; }
        public ICollection<SavedFeature> SavedFeatures { get; set; } = new List<SavedFeature>();
    }
}