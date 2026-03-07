using System.ComponentModel.DataAnnotations;

namespace Portfolio.Common.DTOs;

/// <summary>
/// User preferences submitted from the UI sliders.
/// </summary>
public class HomeSearchPreferencesDto : IValidatableObject
{
    // Budget
    public decimal MaxPrice { get; set; } = 800_000;
    public decimal MaxMonthlyBudget { get; set; } = 4_000;

    // Property filters
    public int MinBedrooms { get; set; } = 2;
    public int MinBathrooms { get; set; } = 1;
    public int MinSqft { get; set; } = 800;
    public int MaxCommuteMin { get; set; } = 45;

    // Weighting (0.0 – 1.0, should sum to ~1.0)
    [Range(0.0, 1.0)]
    public double WeightAffordability { get; set; } = 0.20;
    [Range(0.0, 1.0)]
    public double WeightNeighborhood { get; set; } = 0.15;
    [Range(0.0, 1.0)]
    public double WeightSize { get; set; } = 0.10;
    [Range(0.0, 1.0)]
    public double WeightAppreciation { get; set; } = 0.10;
    [Range(0.0, 1.0)]
    public double WeightCondition { get; set; } = 0.10;
    [Range(0.0, 1.0)]
    public double WeightCommute { get; set; } = 0.10;
    [Range(0.0, 1.0)]
    public double WeightAmenities { get; set; } = 0.10;
    [Range(0.0, 1.0)]
    public double WeightTaxUtilities { get; set; } = 0.05;
    [Range(0.0, 1.0)]
    public double WeightResale { get; set; } = 0.05;
    [Range(0.0, 1.0)]
    public double WeightEnvironment { get; set; } = 0.05;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var sum = WeightAffordability + WeightNeighborhood + WeightSize +
                  WeightAppreciation + WeightCondition + WeightCommute +
                  WeightAmenities + WeightTaxUtilities + WeightResale +
                  WeightEnvironment;

        // Allow a small floating-point tolerance (±0.01)
        if (Math.Abs(sum - 1.0) > 0.01)
        {
            yield return new ValidationResult(
                $"Weights must sum to 100%. Current total: {sum * 100:F0}%.",
                new[] { nameof(WeightAffordability) });
        }
    }
}