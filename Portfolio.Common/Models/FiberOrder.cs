namespace Portfolio.Common.Models;

public class FiberOrder
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public int ClientId { get; set; }
    public FiberClient Client { get; set; } = null!;
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalValue { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? ShipDate { get; set; }
    public ICollection<FiberShipment> Shipments { get; set; } = new List<FiberShipment>();
}
