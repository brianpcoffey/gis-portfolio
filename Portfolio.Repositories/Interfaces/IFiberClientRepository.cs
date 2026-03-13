using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces;

public interface IFiberClientRepository
{
    Task<List<FiberClient>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<FiberClient?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default);
    Task<FiberClient> AddAsync(FiberClient client, CancellationToken cancellationToken = default);
    Task<FiberClient> UpdateAsync(FiberClient client, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default);
}
