using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Manages the current user's saved map features.
    /// </summary>
    public interface ISavedFeatureService
    {
        /// <summary>Returns all saved features for the current user.</summary>
        Task<List<SavedFeatureDto>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>Saves a new feature and returns the persisted record.</summary>
        Task<SavedFeatureDto> CreateAsync(CreateSavedFeatureDto dto, CancellationToken cancellationToken = default);

        /// <summary>Deletes the saved feature by its database id.</summary>
        /// <returns><c>true</c> if a row was deleted; otherwise <c>false</c>.</returns>
        Task<bool> DeleteByDbIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Deletes the saved feature by its feature key.</summary>
        /// <returns><c>true</c> if a row was deleted; otherwise <c>false</c>.</returns>
        Task<bool> DeleteByFeatureKeyAsync(string featureKey, CancellationToken cancellationToken = default);
    }
}