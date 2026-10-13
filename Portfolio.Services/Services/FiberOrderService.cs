using System.Globalization;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services;

public class FiberOrderService : IFiberOrderService
{
    private readonly IFiberOrderRepository _orderRepo;
    private readonly IUserProfileService _userProfileService;
    private readonly TimeProvider _timeProvider;

    public FiberOrderService(
        IFiberOrderRepository orderRepo,
        IUserProfileService userProfileService,
        TimeProvider timeProvider)
    {
        _orderRepo = orderRepo;
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
        var order = new FiberOrder
        {
            UserId = userId,
            OrderNumber = await NextOrderNumberAsync(userId, cancellationToken),
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

    // Order numbers read ORD-<yyyyMMdd>-<NNNN>: the UTC day the order is created (from the injected TimeProvider)
    // and a per-user, per-day sequence that starts at 0001 (it widens past 9999 rather than wrapping). The next
    // sequence is one past the highest the user already holds for that day, so deleting an earlier order never lets
    // a later create reuse a surviving order's number. The seeded demo orders follow the same scheme, dated by their
    // own order date (FiberDemoSeedData). There is no unique constraint on the column, so two creates by the same
    // user racing in the same instant could be issued the same number; the dashboard issues one request at a time.
    private async Task<string> NextOrderNumberAsync(Guid userId, CancellationToken cancellationToken)
    {
        var prefix = $"ORD-{_timeProvider.GetUtcNow().ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-";
        var issued = await _orderRepo.GetOrderNumbersAsync(userId, prefix, cancellationToken);
        var highest = 0;
        foreach (var number in issued)
        {
            if (number.Length > prefix.Length
                && number.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(number.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
                && sequence > highest)
            {
                highest = sequence;
            }
        }

        return $"{prefix}{(highest + 1).ToString("D4", CultureInfo.InvariantCulture)}";
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
