using System.ComponentModel.DataAnnotations;

namespace Portfolio.Common.Models
{
    public class SavedFeature
    {
        public int Id { get; set; }

        [Required]
        public string LayerId { get; set; } = string.Empty;

        [Required]
        public string FeatureId { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string GeometryJson { get; set; } = string.Empty;

        public DateTime DateSaved { get; set; } = DateTime.UtcNow;

        public ICollection<UserNote>? UserNotes { get; set; }
    }
}