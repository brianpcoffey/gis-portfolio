using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services;

public class FiberOrderService : IFiberOrderService
{
    private readonly IFiberOrderRepository _orderRepo;
    private readonly IFiberClientRepository _clientRepo;
    private readonly IUserProfileService _userProfileService;
    private readonly TimeProvider _timeProvider;

    public FiberOrderService(
        IFiberOrderRepository orderRepo,
        IFiberClientRepository clientRepo,
        IUserProfileService userProfileService,
        TimeProvider timeProvider)
    {
        _orderRepo = orderRepo;
        _clientRepo = clientRepo;
        _userProfileService = userProfileService;
        _timeProvider = timeProvider;
    }

    private Guid CurrentUserId =>
        _userProfileService.GetCurrentUserId() ?? throw new InvalidOperationException("User not identified");

    public async Task<List<FiberOrderDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepo.GetAllAsync(CurrentUserId, cancellationToken);
        return orders.Select(MapToDto).ToList();
    }

    public async Task<FiberOrderDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepo.GetByIdAsync(id, CurrentUserId, cancellationToken);
        return order == null ? null : MapToDto(order);
    }

    public async Task<FiberOrderDto> CreateAsync(CreateFiberOrderDto dto, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        // OrderNumber will be generated in repository or here if needed
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var order = new FiberOrder
        {
            UserId = userId,
            ClientName = dto.ClientName,
            ProductName = dto.ProductName,
            Quantity = dto.Quantity,
            UnitPrice = dto.UnitPrice,
            Status = dto.Status,
            OrderDate = dto.OrderDate,
            ShipDate = dto.ShipDate,
        };
        var created = await _orderRepo.AddAsync(order, cancellationToken);
        return MapToDto(created);
    }

    public async Task<FiberOrderDto> UpdateAsync(int id, UpdateFiberOrderDto dto, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        var order = await _orderRepo.GetByIdAsync(id, userId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order {id} not found");
        if (dto.ClientName != null) order.ClientName = dto.ClientName;
        if (dto.ProductName != null) order.ProductName = dto.ProductName;
        if (dto.Quantity.HasValue) order.Quantity = dto.Quantity.Value;
        if (dto.UnitPrice.HasValue) order.UnitPrice = dto.UnitPrice.Value;
        if (dto.Status != null) order.Status = dto.Status;
        if (dto.OrderDate.HasValue) order.OrderDate = dto.OrderDate.Value;
        if (dto.ShipDate.HasValue) order.ShipDate = dto.ShipDate.Value;
        if (dto.ClientLat.HasValue) order.ClientLat = dto.ClientLat.Value;
        if (dto.ClientLng.HasValue) order.ClientLng = dto.ClientLng.Value;
        var updated = await _orderRepo.UpdateAsync(order, cancellationToken);
        return MapToDto(updated);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _orderRepo.DeleteAsync(id, CurrentUserId, cancellationToken);
    }

    private static FiberOrderDto MapToDto(FiberOrder o) => new()
    {
        Id = o.Id,
        OrderNumber = o.OrderNumber,
        ClientName = o.ClientName,
        ProductName = o.ProductName,
        Quantity = o.Quantity,
        UnitPrice = o.UnitPrice,
        Status = o.Status,
        OrderDate = o.OrderDate,
        ShipDate = o.ShipDate,
        ClientLat = o.ClientLat,
        ClientLng = o.ClientLng
    };
}
