using System.ComponentModel.DataAnnotations;

namespace Portfolio.Common.DTOs;

/// <summary>
/// An inventory material (SKU) tracked in the fiber plant, with stock and cost figures.
/// Also used as the create/update request body; validation attributes apply on binding.
/// </summary>
public class FiberMaterialDto
{
    /// <summary>Unique identifier of the material.</summary>
    public int Id { get; set; }

    /// <summary>Human-readable material name.</summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Stock-keeping unit code identifying the material.</summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Sku { get; set; } = string.Empty;

    /// <summary>Category the material is grouped under.</summary>
    [StringLength(100)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Quantity currently in stock.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Quantity on hand cannot be negative.")]
    public decimal QtyOnHand { get; set; }

    /// <summary>Cost per unit of the material.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Unit cost cannot be negative.")]
    public decimal UnitCost { get; set; }

    /// <summary>Total value of stock on hand (quantity multiplied by unit cost).</summary>
    public decimal TotalValue { get; set; }

    /// <summary>True when quantity on hand is at or below the reorder point.</summary>
    public bool IsLowStock { get; set; }

    /// <summary>Stock level at which the material should be reordered.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Reorder point cannot be negative.")]
    public decimal ReorderPoint { get; set; }
}

/// <summary>
/// Request body for creating a fiber material. Excludes server-computed/read-only fields
/// (Id, TotalValue, IsLowStock) so they cannot be over-posted.
/// </summary>
public class CreateFiberMaterialDto
{
    /// <summary>Human-readable material name.</summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Stock-keeping unit code identifying the material.</summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Sku { get; set; } = string.Empty;

    /// <summary>Category the material is grouped under.</summary>
    [StringLength(100)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Quantity currently in stock.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Quantity on hand cannot be negative.")]
    public decimal QtyOnHand { get; set; }

    /// <summary>Cost per unit of the material.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Unit cost cannot be negative.")]
    public decimal UnitCost { get; set; }

    /// <summary>Stock level at which the material should be reordered.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Reorder point cannot be negative.")]
    public decimal ReorderPoint { get; set; }
}

/// <summary>
/// Request body for updating a fiber material (full replace of editable fields).
/// </summary>
public class UpdateFiberMaterialDto : CreateFiberMaterialDto
{
}

/// <summary>
/// Request body for receiving additional stock of a material into inventory.
/// </summary>
public class ReceiveStockDto
{
    /// <summary>Quantity of stock being received (must be positive).</summary>
    [Range(0.0001, double.MaxValue, ErrorMessage = "Received quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    /// <summary>Optional notes about the received stock (e.g. supplier or lot).</summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}
