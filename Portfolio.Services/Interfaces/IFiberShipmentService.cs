using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces;

public interface IFiberShipmentService
{
    Task<List<FiberShipmentDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<FiberShipmentDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<FiberShipmentDto> CreateAsync(FiberShipmentDto dto, CancellationToken cancellationToken = default);
    Task<FiberShipmentDto> UpdateAsync(int id, FiberShipmentDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<FiberShipmentDto> UpdateStatusAsync(int id, UpdateShipmentStatusDto dto, CancellationToken cancellationToken = default);
}
