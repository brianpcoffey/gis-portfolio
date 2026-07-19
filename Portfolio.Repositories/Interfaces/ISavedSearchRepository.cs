using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces
{
    public interface ISavedSearchRepository
    {
        // All lookups/deletes are owner-scoped (userId) to enforce object-level
        // authorization. No unscoped by-id overloads exist by design, so a caller
        // cannot accidentally read or delete another user's saved search.
        Task<List<SavedSearch>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<SavedSearch?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default);
        Task<bool> ExistsByNameAsync(Guid userId, string name, CancellationToken cancellationToken = default);
        Task<SavedSearch> AddAsync(SavedSearch savedSearch, CancellationToken cancellationToken = default);
        Task<SavedSearch> UpdateAsync(SavedSearch savedSearch, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default);
    }
}