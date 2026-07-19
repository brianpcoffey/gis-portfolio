namespace Portfolio.Common.DTOs;

/// <summary>
/// Request DTO for creating a saved Home Finder search.
/// </summary>
public class CreateSavedSearchDto
{
    /// <summary>Optional name to label the saved search; a default may be used when omitted.</summary>
    public string? Name { get; set; }

    /// <summary>Scoring preferences to persist with the search.</summary>
    public HomeSearchPreferencesDto Preferences { get; set; } = new();

    /// <summary>Ids of the property results to associate with the saved search, if any.</summary>
    public int[]? PropertyIds { get; set; }
}
