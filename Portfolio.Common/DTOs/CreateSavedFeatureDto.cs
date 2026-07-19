namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// Request body for saving a map feature to the current user's saved features.
    /// </summary>
    public class CreateSavedFeatureDto
    {
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

        /// <summary>Optional id of the collection to add the feature to.</summary>
        public int? CollectionId { get; set; }
    }
}