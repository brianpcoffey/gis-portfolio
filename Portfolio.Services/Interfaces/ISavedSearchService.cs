using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    public interface ISavedSearchService
    {
        Task<SavedSearchDto> CreateSavedSearchAsync(CreateSavedSearchDto dto, Guid userId, CancellationToken cancellationToken = default);
        Task<List<SavedSearchDto>> GetSavedSearchesAsync(Guid userId, CancellationToken cancellationToken = default);
        Task DeleteSavedSearchAsync(int id, CancellationToken cancellationToken = default);
    }
}