using System.ComponentModel.DataAnnotations;

namespace Portfolio.Common.Configuration;

/// <summary>
/// Strongly-typed configuration for batch geocoding, bound from the "BatchGeocoding" section.
/// </summary>
public class BatchGeocodingOptions
{
    public const string SectionName = "BatchGeocoding";

    /// <summary>Maximum parallel geocoding workers per job.</summary>
    [Range(1, 64)]
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>Minimum ArcGIS match score (0–100) for a row to count as matched.</summary>
    [Range(0, 100)]
    public double MinMatchScore { get; set; } = 80.0;

    /// <summary>How long a geocode cache entry lives, in minutes.</summary>
    [Range(1, 1440)]
    public int CacheTtlMinutes { get; set; } = 60;
}
