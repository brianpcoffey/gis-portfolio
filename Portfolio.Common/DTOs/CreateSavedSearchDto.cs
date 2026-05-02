namespace Portfolio.Common.DTOs;

/// <summary>
/// Request DTO for creating a saved Home Finder search.
/// </summary>
public class CreateSavedSearchDto
{
    public string? Name { get; set; }
    public HomeSearchPreferencesDto Preferences { get; set; } = new();
    public int[]? PropertyIds { get; set; }
}
