namespace Portfolio.Common.DTOs;

public class FiberDashboardDto
{
    public int ActiveShipments { get; set; }
    public int OpenOrders { get; set; }
    public int LowStockAlerts { get; set; }
    public decimal MtdRevenue { get; set; }
    public List<RevenueByMonthDto> RevenueByMonth { get; set; } = new();
    public List<OrdersByStatusDto> OrdersByStatus { get; set; } = new();
    public List<TopClientDto> TopClients { get; set; } = new();
    public List<InventoryByCategoryDto> InventoryByCategory { get; set; } = new();
}

public class InventoryByCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class RevenueByMonthDto
{
    public string Month { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class OrdersByStatusDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class TopClientDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}
