using System;

namespace Portfolio.Common.DTOs
{
    public class UserNoteDto
    {
        public int Id { get; set; }
        public int SavedFeatureId { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}