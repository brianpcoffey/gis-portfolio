using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces;

public interface IFiberOrderRepository
{
    Task<List<FiberOrder>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<FiberOrder?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default);
    /// <summary>The order numbers the user already holds that start with <paramref name="prefix"/> (ordinal match).</summary>
    Task<List<string>> GetOrderNumbersAsync(Guid userId, string prefix, CancellationToken cancellationToken = default);
    /// <summary>
    /// Inserts <paramref name="order"/>. Returns <c>null</c> (and leaves nothing tracked) when the user already holds an
    /// order with the same order number, so the caller can issue a fresh number and try again.
    /// </summary>
    Task<FiberOrder?> TryAddAsync(FiberOrder order, CancellationToken cancellationToken = default);
    Task<FiberOrder> UpdateAsync(FiberOrder order, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default);
}
