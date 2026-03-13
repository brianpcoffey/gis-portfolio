using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces;

public interface IFiberShipmentService
{
    Task<List<FiberShipmentDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<FiberShipmentDto> UpdateStatusAsync(int id, UpdateShipmentStatusDto dto, CancellationToken cancellationToken = default);
}
