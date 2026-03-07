namespace Portfolio.Common.DTOs;

/// <summary>
/// User preferences submitted from the UI sliders.
/// </summary>
public class HomeSearchPreferencesDto
{
    // Budget
    public decimal MaxPrice { get; set; } = 800_000;
    public decimal MaxMonthlyBudget { get; set; } = 4_000;

    // Property filters
    public int MinBedrooms { get; set; } = 2;
    public int MinBathrooms { get; set; } = 1;
    public int MinSqft { get; set; } = 800;
    public int MaxCommuteMin { get; set; } = 45;

    // Weighting (0.0 – 1.0, should sum to ~1.0 but normalized internally)
    public double WeightAffordability { get; set; } = 0.20;
    public double WeightNeighborhood { get; set; } = 0.15;
    public double WeightSize { get; set; } = 0.10;
    public double WeightAppreciation { get; set; } = 0.10;
    public double WeightCondition { get; set; } = 0.10;
    public double WeightCommute { get; set; } = 0.10;
    public double WeightAmenities { get; set; } = 0.10;
    public double WeightTaxUtilities { get; set; } = 0.05;
    public double WeightResale { get; set; } = 0.05;
    public double WeightEnvironment { get; set; } = 0.05;
}