using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    public interface ICollectionService
    {
        Task<List<CollectionDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<CollectionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<CollectionDto> CreateAsync(CollectionCreateDto dto, CancellationToken cancellationToken = default);
        Task<CollectionDto> UpdateAsync(int id, CollectionUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}