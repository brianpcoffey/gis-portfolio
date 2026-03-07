namespace Portfolio.Common.DTOs;

/// <summary>
/// Property with computed composite score returned to the frontend.
/// </summary>
public class ScoredPropertyDto
{
    public int PropertyId { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public int LotSqft { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // Monthly cost breakdown
    public decimal EstimatedMonthlyCost { get; set; }

    // Sub-scores (0–100)
    public double AffordabilityScore { get; set; }
    public double NeighborhoodScore { get; set; }
    public double SizeScore { get; set; }
    public double AppreciationScore { get; set; }
    public double ConditionScore { get; set; }
    public double CommuteScore { get; set; }
    public double AmenitiesScore { get; set; }
    public double TaxUtilitiesScore { get; set; }
    public double ResaleScore { get; set; }
    public double EnvironmentScore { get; set; }

    // Final weighted composite (0–100)
    public double CompositeScore { get; set; }
    public int Rank { get; set; }
}