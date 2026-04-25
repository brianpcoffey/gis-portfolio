using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces;

public interface IFiberOrderService
{
    Task<List<FiberOrderDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<FiberOrderDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<FiberOrderDto> CreateAsync(CreateFiberOrderDto dto, CancellationToken cancellationToken = default);
    Task<FiberOrderDto> UpdateAsync(int id, UpdateFiberOrderDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
