namespace Portfolio.Common.DTOs;

public class FiberShipmentDto
{
    public int Id { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string TrackingNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ShipDate { get; set; }
    public DateTime EstimatedArrival { get; set; }
    public double OriginLat { get; set; }
    public double OriginLng { get; set; }
    public double DestinationLat { get; set; }
    public double DestinationLng { get; set; }
    public string DestinationCity { get; set; } = string.Empty;
    public string DestinationState { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public string ClientName { get; set; } = string.Empty;
}

public class UpdateShipmentStatusDto
{
    public string Status { get; set; } = string.Empty;
}
