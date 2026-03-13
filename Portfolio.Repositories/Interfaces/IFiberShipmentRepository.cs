using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces;

public interface IFiberShipmentRepository
{
    Task<List<FiberShipment>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<FiberShipment?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default);
    Task<FiberShipment> AddAsync(FiberShipment shipment, CancellationToken cancellationToken = default);
    Task<FiberShipment> UpdateStatusAsync(int id, string status, Guid userId, CancellationToken cancellationToken = default);
}
