using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces
{
    public interface ISavedFeatureRepository
    {
        Task<List<SavedFeature>> GetAllAsync();
        Task<SavedFeature?> GetByIdAsync(int id);
        Task<SavedFeature?> GetByLayerAndFeatureIdAsync(string layerId, string featureId);
        Task<SavedFeature> AddAsync(SavedFeature feature);
        Task<bool> DeleteAsync(int id);
        Task<SavedFeature?> GetByFeatureKeyAsync(string featureKey);
    }
}