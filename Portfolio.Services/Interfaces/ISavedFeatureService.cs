using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    public interface ISavedFeatureService
    {
        Task<List<SavedFeatureDto>> GetAllAsync();
        Task<SavedFeatureDto> CreateAsync(CreateSavedFeatureDto dto);
        Task<bool> DeleteByDbIdAsync(int id);
        Task<bool> DeleteByFeatureKeyAsync(string featureKey);
    }
}