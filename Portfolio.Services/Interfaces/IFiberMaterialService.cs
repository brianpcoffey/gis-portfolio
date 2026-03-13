using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces;

public interface IFiberMaterialService
{
    Task<List<FiberMaterialDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<FiberMaterialDto> ReceiveStockAsync(int id, ReceiveStockDto dto, CancellationToken cancellationToken = default);
}
