namespace Portfolio.Common.DTOs;

public class FiberOrderDto
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalValue { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? ShipDate { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public double ClientLat { get; set; }
    public double ClientLng { get; set; }
}

public class CreateFiberOrderDto
{
    public int ClientId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? ShipDate { get; set; }
}

public class UpdateFiberOrderDto
{
    public string? ProductName { get; set; }
    public string? ProductSku { get; set; }
    public int? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Status { get; set; }
    public DateTime? OrderDate { get; set; }
    public DateTime? ShipDate { get; set; }
}
