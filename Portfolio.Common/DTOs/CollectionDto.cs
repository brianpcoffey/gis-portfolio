using System.ComponentModel.DataAnnotations;

namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// A named, color-coded collection (folder) that groups a user's saved features.
    /// </summary>
    public class CollectionDto
    {
        /// <summary>Unique identifier of the collection.</summary>
        public int Id { get; set; }

        /// <summary>Display name of the collection.</summary>
        public string Name { get; set; } = null!;

        /// <summary>Hex color used to render the collection in the UI (defaults to grey).</summary>
        public string Color { get; set; } = "#6c757d";

        /// <summary>UTC timestamp when the collection was created.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>UTC timestamp of the most recent update, or null if never modified.</summary>
        public DateTime? LastModified { get; set; }
    }

    /// <summary>
    /// Request body for creating a new collection.
    /// </summary>
    public class CollectionCreateDto
    {
        /// <summary>Display name for the new collection.</summary>
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = null!;

        /// <summary>Optional hex color for the collection; a default is used when omitted.</summary>
        [StringLength(50)]
        public string? Color { get; set; }
    }

    /// <summary>
    /// Request body for updating an existing collection; only supplied fields are changed.
    /// </summary>
    public class CollectionUpdateDto
    {
        /// <summary>New display name, or null to leave the name unchanged.</summary>
        [StringLength(100, MinimumLength = 1)]
        public string? Name { get; set; }

        /// <summary>New hex color, or null to leave the color unchanged.</summary>
        [StringLength(50)]
        public string? Color { get; set; }
    }
}
