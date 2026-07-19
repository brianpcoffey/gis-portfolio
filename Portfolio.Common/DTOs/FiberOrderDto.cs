using System.ComponentModel.DataAnnotations;

namespace Portfolio.Common.DTOs;

/// <summary>
/// A fiber sales order including client, product, pricing, and fulfillment dates.
/// </summary>
public class FiberOrderDto
{
    /// <summary>Unique identifier of the order.</summary>
    public int Id { get; set; }

    /// <summary>Human-readable order number.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>Name of the client the order is for.</summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>Name of the ordered product.</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Number of units ordered.</summary>
    public int Quantity { get; set; }

    /// <summary>Price charged per unit.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Current fulfillment status of the order.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Date the order was placed.</summary>
    public DateTime OrderDate { get; set; }

    /// <summary>Date the order is scheduled to ship.</summary>
    public DateTime ShipDate { get; set; }

    /// <summary>WGS84 latitude of the client's location, if known.</summary>
    public double? ClientLat { get; set; }

    /// <summary>WGS84 longitude of the client's location, if known.</summary>
    public double? ClientLng { get; set; }
}

/// <summary>
/// Request body for creating a new fiber sales order.
/// </summary>
public class CreateFiberOrderDto
{
    /// <summary>Name of the client the order is for.</summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string ClientName { get; set; } = string.Empty;

    /// <summary>Name of the ordered product.</summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Number of units ordered.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; }

    /// <summary>Price charged per unit.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Unit price cannot be negative.")]
    public decimal UnitPrice { get; set; }

    /// <summary>Initial fulfillment status of the order.</summary>
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Status { get; set; } = string.Empty;

    /// <summary>Date the order was placed.</summary>
    public DateTime OrderDate { get; set; }

    /// <summary>Date the order is scheduled to ship.</summary>
    public DateTime ShipDate { get; set; }
}

/// <summary>
/// Request body for updating an existing order; only non-null fields are applied.
/// </summary>
public class UpdateFiberOrderDto
{
    /// <summary>New client name, or null to leave unchanged.</summary>
    [StringLength(200, MinimumLength = 1)]
    public string? ClientName { get; set; }

    /// <summary>New product name, or null to leave unchanged.</summary>
    [StringLength(200, MinimumLength = 1)]
    public string? ProductName { get; set; }

    /// <summary>New quantity, or null to leave unchanged.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int? Quantity { get; set; }

    /// <summary>New unit price, or null to leave unchanged.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Unit price cannot be negative.")]
    public decimal? UnitPrice { get; set; }

    /// <summary>New status, or null to leave unchanged.</summary>
    [StringLength(50, MinimumLength = 1)]
    public string? Status { get; set; }

    /// <summary>New order date, or null to leave unchanged.</summary>
    public DateTime? OrderDate { get; set; }

    /// <summary>New ship date, or null to leave unchanged.</summary>
    public DateTime? ShipDate { get; set; }

    /// <summary>New client latitude, or null to leave unchanged.</summary>
    public double? ClientLat { get; set; }

    /// <summary>New client longitude, or null to leave unchanged.</summary>
    public double? ClientLng { get; set; }
}
