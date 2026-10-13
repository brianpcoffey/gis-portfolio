using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces;

public interface IFiberOrderRepository
{
    Task<List<FiberOrder>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<FiberOrder?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default);
    /// <summary>The order numbers the user already holds that start with <paramref name="prefix"/> (ordinal match).</summary>
    Task<List<string>> GetOrderNumbersAsync(Guid userId, string prefix, CancellationToken cancellationToken = default);
    Task<FiberOrder> AddAsync(FiberOrder order, CancellationToken cancellationToken = default);
    Task<FiberOrder> UpdateAsync(FiberOrder order, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default);
}
