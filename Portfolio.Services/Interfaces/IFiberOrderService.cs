using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces;

/// <summary>
/// Provides CRUD operations for fiber material orders.
/// </summary>
public interface IFiberOrderService
{
    /// <summary>Returns all fiber orders.</summary>
    Task<List<FiberOrderDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the fiber order with the given id, or <c>null</c> if not found.</summary>
    Task<FiberOrderDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new fiber order and returns the persisted record.</summary>
    Task<FiberOrderDto> CreateAsync(CreateFiberOrderDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates the fiber order with the given id and returns the updated record.</summary>
    Task<FiberOrderDto> UpdateAsync(int id, UpdateFiberOrderDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deletes the fiber order with the given id.</summary>
    /// <returns><c>true</c> if a row was deleted; otherwise <c>false</c>.</returns>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
