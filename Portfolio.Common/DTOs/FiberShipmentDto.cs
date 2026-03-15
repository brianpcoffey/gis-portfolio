namespace Portfolio.Common.DTOs;

public class FiberShipmentDto
{
    public int Id { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public string DestinationState { get; set; } = string.Empty;
    public double OriginLat { get; set; }
    public double OriginLng { get; set; }
    public double DestinationLat { get; set; }
    public double DestinationLng { get; set; }
    public DateTime EstimatedArrival { get; set; }
    public List<RoutePointDto> Route { get; set; } = new();
}

public class RoutePointDto
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public class UpdateShipmentStatusDto
{
    public string Status { get; set; } = string.Empty;
}
