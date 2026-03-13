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

    public FiberDashboardService(
        IFiberOrderRepository orderRepo,
        IFiberShipmentRepository shipmentRepo,
        IFiberMaterialRepository materialRepo,
        IUserProfileService userProfileService)
    {
        _orderRepo = orderRepo;
        _shipmentRepo = shipmentRepo;
        _materialRepo = materialRepo;
        _userProfileService = userProfileService;
    }

    private Guid CurrentUserId =>
        _userProfileService.GetCurrentUserId() ?? throw new InvalidOperationException("User not identified");

    public async Task<FiberDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepo.GetAllAsync(CurrentUserId, cancellationToken);
        var shipments = await _shipmentRepo.GetAllAsync(CurrentUserId, cancellationToken);
        var materials = await _materialRepo.GetAllAsync(CurrentUserId, cancellationToken);
        var now = DateTime.UtcNow;
        var mtdStart = new DateTime(now.Year, now.Month, 1);
        var mtdRevenue = orders.Where(o => o.OrderDate >= mtdStart).Sum(o => o.TotalValue);
        var revenueByMonth = orders.GroupBy(o => o.OrderDate.ToString("yyyy-MM"))
            .Select(g => new RevenueByMonthDto
            {
                Month = g.Key,
                Revenue = g.Sum(x => x.TotalValue)
            }).OrderBy(x => x.Month).ToList();
        var ordersByStatus = orders.GroupBy(o => o.Status)
            .Select(g => new OrdersByStatusDto
            {
                Status = g.Key,
                Count = g.Count()
            }).ToList();
        var topClients = orders.GroupBy(o => o.Client?.Name ?? "")
            .Select(g => new TopClientDto
            {
                ClientName = g.Key,
                TotalValue = g.Sum(x => x.TotalValue)
            })
            .OrderByDescending(x => x.TotalValue)
            .Take(5)
            .ToList();
        return new FiberDashboardDto
        {
            ActiveShipments = shipments.Count(s => s.Status == "In Transit"),
            OpenOrders = orders.Count(o => o.Status != "Delivered"),
            LowStockAlerts = materials.Count(m => m.QtyOnHand < m.ReorderPoint),
            MtdRevenue = mtdRevenue,
            RevenueByMonth = revenueByMonth,
            OrdersByStatus = ordersByStatus,
            TopClients = topClients
        };
    }
}
