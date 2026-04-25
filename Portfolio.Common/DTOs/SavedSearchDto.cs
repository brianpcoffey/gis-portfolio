namespace Portfolio.Common.DTOs;

/// <summary>
/// Response DTO representing a user's saved search.
/// </summary>
public class SavedSearchDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public HomeSearchPreferencesDto? Preferences { get; set; }
    public int[]? PropertyIds { get; set; }
}