namespace Portfolio.Common.DTOs;

public class FiberOrderDto
{
    public int Id { get; set; }
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
}

public class CreateFiberOrderDto
{
    public string ClientName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime ShipDate { get; set; }
}

public class UpdateFiberOrderDto
{
    public string? ClientName { get; set; }
    public string? ProductName { get; set; }
    public int? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Status { get; set; }
    public DateTime? OrderDate { get; set; }
    public DateTime? ShipDate { get; set; }
    public double? ClientLat { get; set; }
    public double? ClientLng { get; set; }
}
