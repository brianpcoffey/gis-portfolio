using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces;

public interface IFiberInventoryTransactionRepository
{
    Task<FiberInventoryTransaction> AddAsync(FiberInventoryTransaction transaction, CancellationToken cancellationToken = default);
    Task<List<FiberInventoryTransaction>> GetByMaterialAsync(int materialId, Guid userId, CancellationToken cancellationToken = default);
}
