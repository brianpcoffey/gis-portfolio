using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Manages users' saved property searches.
    /// </summary>
    public interface ISavedSearchService
    {
        /// <summary>Creates a new saved search for the given user and returns the persisted record.</summary>
        Task<SavedSearchDto> CreateSavedSearchAsync(CreateSavedSearchDto dto, Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Returns all saved searches belonging to the given user.</summary>
        Task<List<SavedSearchDto>> GetSavedSearchesAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Deletes the saved search with the given id, scoped to its owner. Throws KeyNotFoundException if it does not exist or is not owned by <paramref name="userId"/>.</summary>
        Task DeleteSavedSearchAsync(int id, Guid userId, CancellationToken cancellationToken = default);
    }
}