using Portfolio.Common.DTOs;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services;

public class FiberDashboardService : IFiberDashboardService
{
    private readonly IFiberOrderRepository _orderRepo;
    private readonly IFiberShipmentRepository _shipmentRepo;
    private readonly IFiberMaterialRepository _materialRepo;
    private readonly IUserProfileService _userProfileService;
    private readonly TimeProvider _timeProvider;

    public FiberDashboardService(
        IFiberOrderRepository orderRepo,
        IFiberShipmentRepository shipmentRepo,
        IFiberMaterialRepository materialRepo,
        IUserProfileService userProfileService,
        TimeProvider timeProvider)
    {
        _orderRepo = orderRepo;
        _shipmentRepo = shipmentRepo;
        _materialRepo = materialRepo;
        _userProfileService = userProfileService;
        _timeProvider = timeProvider;
    }

    private Guid CurrentUserId =>
        _userProfileService.GetCurrentUserId() ?? throw new InvalidOperationException("User not identified");

    public async Task<FiberDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepo.GetAllAsync(CurrentUserId, cancellationToken);
        var shipments = await _shipmentRepo.GetAllAsync(CurrentUserId, cancellationToken);
        var materials = await _materialRepo.GetAllAsync(CurrentUserId, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var mtdStart = new DateTime(now.Year, now.Month, 1);
        var mtdRevenue = orders.Where(o => o.OrderDate >= mtdStart).Sum(o => o.UnitPrice * o.Quantity);
        var revenueByMonth = orders
            .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new RevenueByMonthDto
            {
                // Include the year so orders in the same month across different years
                // are not merged into a single, mis-ordered bucket.
                Month = new DateTime(g.Key.Year, g.Key.Month, 1)
                    .ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture),
                Revenue = g.Sum(x => x.UnitPrice * x.Quantity)
            })
            .ToList();
        var allStatuses = new[] { "Draft", "Pending", "Confirmed", "In Production", "Shipped", "Delivered" };
        var ordersByStatus = allStatuses
            .Select(status => new OrdersByStatusDto
            {
                Status = status,
                Count = orders.Count(o => o.Status == status)
            })
            .ToList();
        var currentYear = now.Year;
        var topClients = orders
            .Where(o => o.OrderDate.Year == currentYear)
            .GroupBy(o => o.ClientName ?? "")
            .Select(g => new TopClientDto
            {
                Name = g.Key,
                Revenue = g.Sum(x => x.UnitPrice * x.Quantity)
            })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToList();

        // Inventory by Category analytics
        var inventoryByCategory = materials
            .GroupBy(m => string.IsNullOrEmpty(m.Category) ? "Uncategorized" : m.Category)
            .Select(g => new InventoryByCategoryDto
            {
                Category = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        return new FiberDashboardDto
        {
            ActiveShipments = shipments.Count(s => s.Status == "In Transit"),
            OpenOrders = orders.Count(o => o.Status != "Delivered" && o.Status != "Shipped"),
            LowStockAlerts = materials.Count(m => m.QtyOnHand <= m.ReorderPoint),
            MtdRevenue = mtdRevenue,
            RevenueByMonth = revenueByMonth,
            OrdersByStatus = ordersByStatus,
            TopClients = topClients,
            InventoryByCategory = inventoryByCategory
        };
    }
}
