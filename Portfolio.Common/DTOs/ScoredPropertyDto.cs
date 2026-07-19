namespace Portfolio.Common.DTOs;

/// <summary>
/// Property with computed composite score returned to the frontend.
/// </summary>
public class ScoredPropertyDto
{
    /// <summary>Unique identifier of the property.</summary>
    public int PropertyId { get; set; }
    /// <summary>Street address of the property.</summary>
    public string Street { get; set; } = string.Empty;
    /// <summary>City the property is located in.</summary>
    public string City { get; set; } = string.Empty;
    /// <summary>Postal (ZIP) code of the property.</summary>
    public string ZipCode { get; set; } = string.Empty;
    /// <summary>Listing price of the property.</summary>
    public decimal Price { get; set; }
    /// <summary>Number of bedrooms.</summary>
    public int Bedrooms { get; set; }
    /// <summary>Number of bathrooms.</summary>
    public int Bathrooms { get; set; }
    /// <summary>Lot size, in square feet.</summary>
    public int LotSqft { get; set; }
    /// <summary>WGS84 latitude of the property.</summary>
    public double Latitude { get; set; }
    /// <summary>WGS84 longitude of the property.</summary>
    public double Longitude { get; set; }

    // Monthly cost breakdown
    /// <summary>Estimated total monthly cost of ownership (mortgage, taxes, utilities, and similar).</summary>
    public decimal EstimatedMonthlyCost { get; set; }

    // Sub-scores (0–100)
    /// <summary>Affordability sub-score, from 0 to 100.</summary>
    public double AffordabilityScore { get; set; }
    /// <summary>Neighborhood-quality sub-score, from 0 to 100.</summary>
    public double NeighborhoodScore { get; set; }
    /// <summary>Size and space sub-score, from 0 to 100.</summary>
    public double SizeScore { get; set; }
    /// <summary>Expected-appreciation sub-score, from 0 to 100.</summary>
    public double AppreciationScore { get; set; }
    /// <summary>Property-condition sub-score, from 0 to 100.</summary>
    public double ConditionScore { get; set; }
    /// <summary>Commute sub-score, from 0 to 100.</summary>
    public double CommuteScore { get; set; }
    /// <summary>Nearby-amenities sub-score, from 0 to 100.</summary>
    public double AmenitiesScore { get; set; }
    /// <summary>Taxes-and-utilities sub-score, from 0 to 100.</summary>
    public double TaxUtilitiesScore { get; set; }
    /// <summary>Resale-potential sub-score, from 0 to 100.</summary>
    public double ResaleScore { get; set; }
    /// <summary>Environmental sub-score, from 0 to 100.</summary>
    public double EnvironmentScore { get; set; }

    // Final weighted composite (0–100)
    /// <summary>Final weighted composite score, from 0 to 100.</summary>
    public double CompositeScore { get; set; }
    /// <summary>Rank of this property among the scored results (1 = best).</summary>
    public int Rank { get; set; }
}