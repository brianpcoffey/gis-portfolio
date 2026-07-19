namespace Portfolio.Common.DTOs;

/// <summary>
/// A map feature returned from an ArcGIS layer, including its geometry and attributes.
/// </summary>
public class FeatureDto
{
    /// <summary>Identifier of the map layer the feature belongs to.</summary>
    public string LayerId { get; set; } = string.Empty;

    /// <summary>Identifier of the feature within its layer.</summary>
    public string FeatureId { get; set; } = string.Empty;

    /// <summary>Display name of the feature.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>GeoJSON representation of the feature's geometry.</summary>
    public string GeometryJson { get; set; } = string.Empty;

    /// <summary>
    /// Contains all metadata attributes from the ArcGIS feature layer.
    /// Keys may include STATE_NAME, STATE_ABBR, POPULATION, etc.
    /// </summary>
    public Dictionary<string, object?>? Attributes { get; set; }
}