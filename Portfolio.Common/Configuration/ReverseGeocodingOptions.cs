using System.ComponentModel.DataAnnotations;

namespace Portfolio.Common.Configuration;

/// <summary>
/// Strongly-typed configuration for reverse geocoding, bound from the "ReverseGeocoding" section.
/// </summary>
public class ReverseGeocodingOptions
{
    public const string SectionName = "ReverseGeocoding";

    /// <summary>Grid size (in degrees) that coordinates snap to, enabling spatial cache hits.</summary>
    [Range(0.0001, 1.0)]
    public double GridResolutionDegrees { get; set; } = 0.001;

    /// <summary>Sliding cache expiration for reverse-geocode results, in minutes.</summary>
    [Range(1, 1440)]
    public int CacheSlidingExpirationMinutes { get; set; } = 30;
}
