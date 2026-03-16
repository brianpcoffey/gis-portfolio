using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces;

public interface IFiberMaterialRepository
{
    Task<List<FiberMaterial>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<FiberMaterial?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default);
    Task<FiberMaterial> AddAsync(FiberMaterial material, CancellationToken cancellationToken = default);
    Task<FiberMaterial> UpdateAsync(int id, Portfolio.Common.DTOs.FiberMaterialDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default);
    Task<List<FiberMaterial>> GetLowStockAsync(Guid userId, CancellationToken cancellationToken = default);
}
