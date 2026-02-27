using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces
{
    public interface ISavedFeatureRepository
    {
        Task<List<SavedFeature>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<SavedFeature?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default);
        Task<SavedFeature?> GetByLayerAndFeatureIdAsync(string layerId, string featureId, Guid userId, CancellationToken cancellationToken = default);
        Task<SavedFeature?> GetByFeatureKeyAsync(string featureKey, Guid userId, CancellationToken cancellationToken = default);
        Task<SavedFeature> AddAsync(SavedFeature feature, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string layerId, string featureId, Guid userId, CancellationToken cancellationToken = default);
    }
}