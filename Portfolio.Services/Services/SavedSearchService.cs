using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services
{
    public class SavedSearchService : ISavedSearchService
    {
        private readonly ISavedSearchRepository _repo;

        public SavedSearchService(ISavedSearchRepository repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// Creates a saved search after validating that the name is unique per user.
        /// </summary>
        public async Task<SavedSearch> CreateSavedSearchAsync(SavedSearch savedSearch, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(savedSearch.Name))
                throw new ArgumentException("Name is required.", nameof(savedSearch));

            var duplicate = await _repo.ExistsByNameAsync(savedSearch.UserId, savedSearch.Name, cancellationToken);
            if (duplicate)
                throw new InvalidOperationException($"A saved search with the name '{savedSearch.Name}' already exists.");

            savedSearch.CreatedAt = DateTime.UtcNow;
            return await _repo.AddAsync(savedSearch, cancellationToken);
        }

        /// <summary>
        /// Returns all saved searches for a given user, ordered by most recent.
        /// </summary>
        public async Task<List<SavedSearch>> GetSavedSearchesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _repo.GetAllAsync(userId, cancellationToken);
        }

        /// <summary>
        /// Deletes a saved search by ID. Throws if not found.
        /// </summary>
        public async Task DeleteSavedSearchAsync(int id, CancellationToken cancellationToken = default)
        {
            var exists = await _repo.GetByIdAsync(id, cancellationToken);
            if (exists is null)
                throw new KeyNotFoundException($"Saved search with ID {id} was not found.");

            await _repo.DeleteAsync(id, cancellationToken);
        }
    }
}