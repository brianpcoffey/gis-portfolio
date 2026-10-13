using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces;

public interface IFiberOrderRepository
{
    Task<List<FiberOrder>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<FiberOrder?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default);
    Task<FiberOrder> AddAsync(FiberOrder order, CancellationToken cancellationToken = default);
    Task<FiberOrder> UpdateAsync(FiberOrder order, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default);
}
