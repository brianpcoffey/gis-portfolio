using Portfolio.Common.DTOs;
using System.Threading;

namespace Portfolio.Services.Interfaces
{
    public interface IArcGisService
    {
        Task<List<FeatureDto>> QueryFeaturesAsync(string layerId, string? bbox = null, CancellationToken cancellationToken = default);
        Task<FeatureDto?> GetFeatureAsync(string layerId, string featureId, CancellationToken cancellationToken = default);
    }
}