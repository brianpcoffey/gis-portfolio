namespace Portfolio.Common.Models;

public class FiberMaterial
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
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
    public ICollection<FiberInventoryTransaction> InventoryTransactions { get; set; } = new List<FiberInventoryTransaction>();
}
