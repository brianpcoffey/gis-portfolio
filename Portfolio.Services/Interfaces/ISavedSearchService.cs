using Portfolio.Common.Models;

namespace Portfolio.Services.Interfaces
{
    public interface ISavedSearchService
    {
        Task<SavedSearch> CreateSavedSearchAsync(SavedSearch savedSearch, CancellationToken cancellationToken = default);
        Task<List<SavedSearch>> GetSavedSearchesAsync(Guid userId, CancellationToken cancellationToken = default);
        Task DeleteSavedSearchAsync(int id, CancellationToken cancellationToken = default);
    }
}