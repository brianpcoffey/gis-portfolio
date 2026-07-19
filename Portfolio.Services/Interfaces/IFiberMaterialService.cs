using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces;

/// <summary>
/// Manages fiber material inventory records and stock levels.
/// </summary>
public interface IFiberMaterialService
{
    /// <summary>Returns all fiber materials.</summary>
    Task<List<FiberMaterialDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the fiber material with the given id, or <c>null</c> if not found.</summary>
    Task<FiberMaterialDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new fiber material and returns the persisted record.</summary>
    Task<FiberMaterialDto> CreateAsync(FiberMaterialDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates the fiber material with the given id and returns the updated record.</summary>
    Task<FiberMaterialDto> UpdateAsync(int id, FiberMaterialDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deletes the fiber material with the given id.</summary>
    /// <returns><c>true</c> if a row was deleted; otherwise <c>false</c>.</returns>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Receives stock into inventory for the given material and returns the updated record.</summary>
    Task<FiberMaterialDto> ReceiveStockAsync(int id, ReceiveStockDto dto, CancellationToken cancellationToken = default);
}
