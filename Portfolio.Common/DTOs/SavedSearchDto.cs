namespace Portfolio.Common.DTOs;

/// <summary>
/// Response DTO representing a user's saved search.
/// </summary>
public class SavedSearchDto
{
    /// <summary>Unique identifier of the saved search.</summary>
    public int Id { get; set; }

    /// <summary>User-supplied name for the saved search.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the search was saved.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>The scoring preferences that were used for this search.</summary>
    public HomeSearchPreferencesDto? Preferences { get; set; }

    /// <summary>Ids of the properties captured in the saved search results, if any.</summary>
    public int[]? PropertyIds { get; set; }
}