using Portfolio.Common.Models;
using System.Threading;

namespace Portfolio.Repositories.Interfaces
{
    public interface ICollectionRepository
    {
        Task<List<Collection>> GetAllAsync(Guid ownerId, CancellationToken cancellationToken = default);
        Task<Collection?> GetByIdAsync(int id, Guid ownerId, CancellationToken cancellationToken = default);
        Task<Collection> AddAsync(Collection entity, CancellationToken cancellationToken = default);
        Task<Collection> UpdateAsync(Collection entity, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, Guid ownerId, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(Guid ownerId, string name, CancellationToken cancellationToken = default);
    }
}