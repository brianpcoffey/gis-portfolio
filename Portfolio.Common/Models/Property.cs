namespace Portfolio.Common.Models;

/// <summary>
/// Single-family home in Redlands, CA (mock Kaggle dataset).
/// </summary>
public class Property
{
    public int Id { get; set; }

    // Listing basics
    public string? BrokeredBy { get; set; }
    public string Status { get; set; } = "active";
    public decimal Price { get; set; }
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public decimal AcreLot { get; set; }
    public int LotSqft { get; set; }

    // Address
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = "Redlands";
    public string State { get; set; } = "CA";
    public string ZipCode { get; set; } = string.Empty;

    // GIS coordinates
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // Financial
    public decimal HoaFee { get; set; }
    public decimal PropertyTax { get; set; }
    public decimal Utilities { get; set; }

    // Neighborhood scores (0–100)
    public int SchoolRating { get; set; }
    public int CrimeScore { get; set; }
    public int Walkability { get; set; }
    public int TransitAccess { get; set; }
    public int AmenitiesScore { get; set; }

    // Commute
    public int CommuteMin { get; set; }

    // Property condition
    public int YearBuilt { get; set; }
    public int? LastRenovation { get; set; }
    public int RoofCondition { get; set; }
    public int AcCondition { get; set; }
    public int PlumbingCondition { get; set; }
    public int ElectricalCondition { get; set; }
    public int FloorPlanScore { get; set; }

    // Investment
    public int FutureAppreciation { get; set; }
    public int ResalePotential { get; set; }

    // Environmental
    public int FloodRisk { get; set; }
    public int NoiseLevel { get; set; }
}