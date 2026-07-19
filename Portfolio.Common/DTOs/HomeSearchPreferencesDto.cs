using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Portfolio.Common.DTOs;

/// <summary>
/// User preferences submitted from the UI sliders.
/// </summary>
public class HomeSearchPreferencesDto : IValidatableObject
{
    // Budget
    /// <summary>Maximum purchase price the user will consider.</summary>
    public decimal MaxPrice { get; set; } = 800_000;
    /// <summary>Maximum total monthly housing budget.</summary>
    public decimal MaxMonthlyBudget { get; set; } = 4_000;

    // Property filters
    /// <summary>Minimum number of bedrooms required.</summary>
    public int MinBedrooms { get; set; } = 2;
    /// <summary>Minimum number of bathrooms required.</summary>
    public int MinBathrooms { get; set; } = 1;
    /// <summary>Minimum interior area required, in square feet.</summary>
    public int MinSqft { get; set; } = 800;
    /// <summary>Maximum acceptable commute time, in minutes.</summary>
    public int MaxCommuteMin { get; set; } = 45;

    // Weighting (0.0 – 1.0, should sum to ~1.0)
    /// <summary>Relative importance of affordability in the composite score (0.0 to 1.0).</summary>
    [Range(0.0, 1.0)]
    public double WeightAffordability { get; set; } = 0.20;
    /// <summary>Relative importance of neighborhood quality (0.0 to 1.0).</summary>
    [Range(0.0, 1.0)]
    public double WeightNeighborhood { get; set; } = 0.15;
    /// <summary>Relative importance of property size (0.0 to 1.0).</summary>
    [Range(0.0, 1.0)]
    public double WeightSize { get; set; } = 0.10;
    /// <summary>Relative importance of expected appreciation (0.0 to 1.0).</summary>
    [Range(0.0, 1.0)]
    public double WeightAppreciation { get; set; } = 0.10;
    /// <summary>Relative importance of property condition (0.0 to 1.0).</summary>
    [Range(0.0, 1.0)]
    public double WeightCondition { get; set; } = 0.10;
    /// <summary>Relative importance of commute time (0.0 to 1.0).</summary>
    [Range(0.0, 1.0)]
    public double WeightCommute { get; set; } = 0.10;
    /// <summary>Relative importance of nearby amenities (0.0 to 1.0).</summary>
    [Range(0.0, 1.0)]
    public double WeightAmenities { get; set; } = 0.10;
    /// <summary>Relative importance of taxes and utilities (0.0 to 1.0).</summary>
    [Range(0.0, 1.0)]
    public double WeightTaxUtilities { get; set; } = 0.05;
    /// <summary>Relative importance of resale potential (0.0 to 1.0).</summary>
    [Range(0.0, 1.0)]
    public double WeightResale { get; set; } = 0.05;
    /// <summary>Relative importance of environmental factors (0.0 to 1.0).</summary>
    [Range(0.0, 1.0)]
    public double WeightEnvironment { get; set; } = 0.05;

    /// <summary>All weight values as an enumerable, useful for summing/validation.</summary>
    public IEnumerable<double> AllWeights =>
    [
        WeightAffordability, WeightNeighborhood, WeightSize,
        WeightAppreciation, WeightCondition, WeightCommute,
        WeightAmenities, WeightTaxUtilities, WeightResale,
        WeightEnvironment
    ];

    /// <summary>
    /// Validates that each weight is within range and that all weights sum to ~100%.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var sum = AllWeights.Sum();

        // Enforce each weight individually (defense-in-depth)
        foreach (var w in AllWeights)
        {
            if (w < 0.0 || w > 1.0)
            {
                yield return new ValidationResult(
                    "Each weight must be between 0% and 100%.",
                    [nameof(WeightAffordability)]); // Representative member
                yield break;
            }
        }

        // Allow a small floating-point tolerance (±0.01)
        if (Math.Abs(sum - 1.0) > 0.01)
        {
            var pctSum = sum * 100;
            yield return new ValidationResult(
                $"Weights must total 100%. Current total: {pctSum:F0}%.",
                [
                    nameof(WeightAffordability), nameof(WeightNeighborhood),
                    nameof(WeightSize), nameof(WeightAppreciation),
                    nameof(WeightCondition), nameof(WeightCommute),
                    nameof(WeightAmenities), nameof(WeightTaxUtilities),
                    nameof(WeightResale), nameof(WeightEnvironment)
                ]);
        }
    }
}