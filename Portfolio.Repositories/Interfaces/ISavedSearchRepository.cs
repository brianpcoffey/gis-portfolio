using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces
{
    public interface ISavedSearchRepository
    {
        Task<SavedSearch?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<List<SavedSearch>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<SavedSearch?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default);
        Task<bool> ExistsByNameAsync(Guid userId, string name, CancellationToken cancellationToken = default);
        Task<SavedSearch> AddAsync(SavedSearch savedSearch, CancellationToken cancellationToken = default);
        Task<SavedSearch> UpdateAsync(SavedSearch savedSearch, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}