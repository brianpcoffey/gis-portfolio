using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces;

/// <summary>
/// Manages fiber shipment records and their delivery status.
/// </summary>
public interface IFiberShipmentService
{
    /// <summary>Returns all fiber shipments.</summary>
    Task<List<FiberShipmentDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the fiber shipment with the given id, or <c>null</c> if not found.</summary>
    Task<FiberShipmentDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new fiber shipment and returns the persisted record.</summary>
    Task<FiberShipmentDto> CreateAsync(FiberShipmentDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates the fiber shipment with the given id and returns the updated record.</summary>
    Task<FiberShipmentDto> UpdateAsync(int id, FiberShipmentDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deletes the fiber shipment with the given id.</summary>
    /// <returns><c>true</c> if a row was deleted; otherwise <c>false</c>.</returns>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Updates the delivery status of the given shipment and returns the updated record.</summary>
    Task<FiberShipmentDto> UpdateStatusAsync(int id, UpdateShipmentStatusDto dto, CancellationToken cancellationToken = default);
}
