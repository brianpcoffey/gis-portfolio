namespace Portfolio.Common.DTOs;

public class FiberMaterialDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal QtyOnHand { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal ReorderQty { get; set; }
    public decimal UnitCost { get; set; }
    public string Supplier { get; set; } = string.Empty;
    public string WarehouseLocation { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public decimal TotalValue { get; set; }
    public bool IsLowStock { get; set; }
}

public class ReceiveStockDto
{
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}
