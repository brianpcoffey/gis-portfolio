using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    public interface ISavedFeatureService
    {
        Task<List<SavedFeatureDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<SavedFeatureDto> CreateAsync(CreateSavedFeatureDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteByDbIdAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> DeleteByFeatureKeyAsync(string featureKey, CancellationToken cancellationToken = default);
    }
}