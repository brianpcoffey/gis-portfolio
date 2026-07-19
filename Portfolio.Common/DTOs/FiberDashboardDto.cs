namespace Portfolio.Common.DTOs;

/// <summary>
/// Aggregated metrics and chart series powering the fiber operations dashboard.
/// </summary>
public class FiberDashboardDto
{
    /// <summary>Number of shipments currently in transit.</summary>
    public int ActiveShipments { get; set; }

    /// <summary>Number of orders that are not yet fulfilled.</summary>
    public int OpenOrders { get; set; }

    /// <summary>Number of materials currently at or below their reorder point.</summary>
    public int LowStockAlerts { get; set; }

    /// <summary>Month-to-date revenue total.</summary>
    public decimal MtdRevenue { get; set; }

    /// <summary>Revenue totals broken down by month, for the trend chart.</summary>
    public List<RevenueByMonthDto> RevenueByMonth { get; set; } = new();

    /// <summary>Order counts grouped by status, for the status breakdown chart.</summary>
    public List<OrdersByStatusDto> OrdersByStatus { get; set; } = new();

    /// <summary>Highest-revenue clients, for the leaderboard.</summary>
    public List<TopClientDto> TopClients { get; set; } = new();

    /// <summary>Inventory item counts grouped by category.</summary>
    public List<InventoryByCategoryDto> InventoryByCategory { get; set; } = new();
}

/// <summary>
/// Count of inventory items within a single category.
/// </summary>
public class InventoryByCategoryDto
{
    /// <summary>Inventory category name.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Number of inventory items in the category.</summary>
    public int Count { get; set; }
}

/// <summary>
/// Revenue total for a single month in the revenue trend series.
/// </summary>
public class RevenueByMonthDto
{
    /// <summary>Month label the revenue applies to (e.g. "2026-07").</summary>
    public string Month { get; set; } = string.Empty;

    /// <summary>Total revenue recorded for the month.</summary>
    public decimal Revenue { get; set; }
}

/// <summary>
/// Count of orders in a single status for the order-status breakdown.
/// </summary>
public class OrdersByStatusDto
{
    /// <summary>Order status label (e.g. "Open", "Shipped").</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Number of orders in this status.</summary>
    public int Count { get; set; }
}

/// <summary>
/// A single client's revenue total for the top-clients leaderboard.
/// </summary>
public class TopClientDto
{
    /// <summary>Client name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Total revenue attributed to the client.</summary>
    public decimal Revenue { get; set; }
}
