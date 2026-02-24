using System;
using System.ComponentModel.DataAnnotations;

namespace Portfolio.Common.Models
{
    public class UserNote
    {
        public int Id { get; set; }

        [Required]
        public int SavedFeatureId { get; set; }

        [Required]
        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public SavedFeature? SavedFeature { get; set; }
    }
}