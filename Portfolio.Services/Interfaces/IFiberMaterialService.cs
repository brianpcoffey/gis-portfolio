using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces;

public interface IFiberMaterialService
{
    Task<List<FiberMaterialDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<FiberMaterialDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<FiberMaterialDto> CreateAsync(FiberMaterialDto dto, CancellationToken cancellationToken = default);
    Task<FiberMaterialDto> UpdateAsync(int id, FiberMaterialDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<FiberMaterialDto> ReceiveStockAsync(int id, ReceiveStockDto dto, CancellationToken cancellationToken = default);
}
