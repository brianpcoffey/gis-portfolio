using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces
{
    public interface ICollectionRepository
    {
        Task<List<Collection>> GetAllAsync(string ownerId, CancellationToken cancellationToken = default);
        Task<Collection?> GetByIdAsync(int id, string ownerId, CancellationToken cancellationToken = default);
        Task<Collection> AddAsync(Collection entity, CancellationToken cancellationToken = default);
        Task<Collection> UpdateAsync(Collection entity, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, string ownerId, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string ownerId, string name, CancellationToken cancellationToken = default);
    }
}