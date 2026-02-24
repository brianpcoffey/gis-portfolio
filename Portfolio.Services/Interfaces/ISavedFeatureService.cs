using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    public interface ISavedFeatureService
    {
        Task<List<SavedFeatureDto>> GetAllAsync();
        Task<SavedFeatureDto?> GetByIdAsync(int id);
        Task<SavedFeatureDto> AddAsync(SavedFeatureDto dto);
        Task<SavedFeatureDto> UpdateAsync(SavedFeatureDto dto);
        Task<bool> DeleteAsync(int id);
    }
}