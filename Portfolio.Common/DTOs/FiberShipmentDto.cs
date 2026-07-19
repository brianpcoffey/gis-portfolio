namespace Portfolio.Common.DTOs;

/// <summary>
/// A fiber shipment with carrier, status, origin/destination, and route path.
/// </summary>
public class FiberShipmentDto
{
    /// <summary>Unique identifier of the shipment.</summary>
    public int Id { get; set; }

    /// <summary>Carrier tracking number for the shipment.</summary>
    public string TrackingNumber { get; set; } = string.Empty;

    /// <summary>Name of the carrier handling the shipment.</summary>
    public string CarrierName { get; set; } = string.Empty;

    /// <summary>Current status of the shipment (e.g. "In Transit", "Delivered").</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Destination city.</summary>
    public string DestinationCity { get; set; } = string.Empty;

    /// <summary>Destination state.</summary>
    public string DestinationState { get; set; } = string.Empty;

    /// <summary>WGS84 latitude of the shipment origin.</summary>
    public double OriginLat { get; set; }

    /// <summary>WGS84 longitude of the shipment origin.</summary>
    public double OriginLng { get; set; }

    /// <summary>WGS84 latitude of the shipment destination.</summary>
    public double DestinationLat { get; set; }

    /// <summary>WGS84 longitude of the shipment destination.</summary>
    public double DestinationLng { get; set; }

    /// <summary>Estimated arrival date and time.</summary>
    public DateTime EstimatedArrival { get; set; }

    /// <summary>Ordered list of points describing the shipment's route path.</summary>
    public List<RoutePointDto> Route { get; set; } = new();
}

/// <summary>
/// A single latitude/longitude point along a shipment route.
/// </summary>
public class RoutePointDto
{
    /// <summary>WGS84 latitude of the route point.</summary>
    public double Lat { get; set; }

    /// <summary>WGS84 longitude of the route point.</summary>
    public double Lng { get; set; }
}

/// <summary>
/// Request body for updating a shipment's status.
/// </summary>
public class UpdateShipmentStatusDto
{
    /// <summary>New status to set on the shipment.</summary>
    public string Status { get; set; } = string.Empty;
}
