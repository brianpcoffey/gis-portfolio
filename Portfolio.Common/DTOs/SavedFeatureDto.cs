namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// A map feature that a user has saved, including its collection and timestamps.
    /// </summary>
    public class SavedFeatureDto
    {
        /// <summary>Unique identifier of the saved feature record.</summary>
        public int Id { get; set; }

        /// <summary>Identifier of the map layer the feature belongs to.</summary>
        public string LayerId { get; set; } = string.Empty;

        /// <summary>Identifier of the feature within its layer.</summary>
        public string FeatureId { get; set; } = string.Empty;

        /// <summary>Optional user-supplied display name for the saved feature.</summary>
        public string? Name { get; set; }

        /// <summary>Optional GeoJSON geometry of the feature.</summary>
        public string? GeometryJson { get; set; }

        /// <summary>Optional user-supplied description or notes.</summary>
        public string? Description { get; set; }

        /// <summary>Id of the collection the feature belongs to, or null if uncategorized.</summary>
        public int? CollectionId { get; set; }

        /// <summary>Display name of the collection the feature belongs to, if any.</summary>
        public string? CollectionName { get; set; }

        /// <summary>UTC timestamp when the feature was first saved.</summary>
        public DateTime DateSaved { get; set; }

        /// <summary>UTC timestamp of the most recent update, or null if never modified.</summary>
        public DateTime? LastModified { get; set; }
    }
}