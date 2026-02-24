using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces
{
    public interface ISavedFeatureRepository
    {
        Task<List<SavedFeature>> GetAllAsync();
        Task<SavedFeature?> GetByIdAsync(int id);
        Task<SavedFeature> AddAsync(SavedFeature feature);
        Task<SavedFeature> UpdateAsync(SavedFeature feature);
        Task<bool> DeleteAsync(int id);
    }
}