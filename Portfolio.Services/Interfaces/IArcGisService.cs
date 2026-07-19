using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Queries features from ArcGIS feature layers.
    /// </summary>
    public interface IArcGisService
    {
        /// <summary>Queries features from the given layer, optionally filtered by a bounding box.</summary>
        Task<List<FeatureDto>> QueryFeaturesAsync(string layerId, string? bbox = null, CancellationToken cancellationToken = default);

        /// <summary>Returns a single feature by id, or <c>null</c> if not found.</summary>
        Task<FeatureDto?> GetFeatureAsync(string layerId, string featureId, CancellationToken cancellationToken = default);
    }
}