namespace Portfolio.Common.Models;

public class FiberOrder
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime ShipDate { get; set; }
    public double? ClientLat { get; set; }
    public double? ClientLng { get; set; }
    public ICollection<FiberShipment> Shipments { get; set; } = new List<FiberShipment>();
}
