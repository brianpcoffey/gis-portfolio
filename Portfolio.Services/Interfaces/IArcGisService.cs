using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    public interface IArcGisService
    {
        Task<List<FeatureDto>> QueryFeaturesAsync(string layerId, string? bbox = null);
        Task<FeatureDto?> GetFeatureAsync(string layerId, string featureId);
    }
}