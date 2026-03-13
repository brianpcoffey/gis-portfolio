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
        var client = await _clientRepo.GetByIdAsync(dto.ClientId, userId, cancellationToken)
            ?? throw new ArgumentException("Client not found");
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var order = new FiberOrder
        {
            UserId = userId,
            ClientId = dto.ClientId,
            ProductName = dto.ProductName,
            ProductSku = dto.ProductSku,
            Quantity = dto.Quantity,
            UnitPrice = dto.UnitPrice,
            TotalValue = dto.UnitPrice * dto.Quantity,
            Status = dto.Status,
            OrderDate = dto.OrderDate,
            ShipDate = dto.ShipDate
        };
        var created = await _orderRepo.AddAsync(order, cancellationToken);
        return MapToDto(created);
    }

    public async Task<FiberOrderDto> UpdateAsync(int id, UpdateFiberOrderDto dto, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        var order = await _orderRepo.GetByIdAsync(id, userId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order {id} not found");
        if (dto.ProductName != null) order.ProductName = dto.ProductName;
        if (dto.ProductSku != null) order.ProductSku = dto.ProductSku;
        if (dto.Quantity.HasValue) order.Quantity = dto.Quantity.Value;
        if (dto.UnitPrice.HasValue) order.UnitPrice = dto.UnitPrice.Value;
        if (dto.Status != null) order.Status = dto.Status;
        if (dto.OrderDate.HasValue) order.OrderDate = dto.OrderDate.Value;
        if (dto.ShipDate.HasValue) order.ShipDate = dto.ShipDate.Value;
        order.TotalValue = order.UnitPrice * order.Quantity;
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
        ProductName = o.ProductName,
        ProductSku = o.ProductSku,
        Quantity = o.Quantity,
        UnitPrice = o.UnitPrice,
        TotalValue = o.TotalValue,
        Status = o.Status,
        OrderDate = o.OrderDate,
        ShipDate = o.ShipDate,
        ClientId = o.ClientId,
        ClientName = o.Client?.Name ?? string.Empty,
        ClientLat = o.Client?.Latitude ?? 0,
        ClientLng = o.Client?.Longitude ?? 0
    };
}
