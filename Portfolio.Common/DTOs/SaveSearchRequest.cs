namespace Portfolio.Common.DTOs;

/// <summary>
/// Request payload for saving a Home Finder search.
/// </summary>
public class SaveSearchRequest
{
    public string? Name { get; set; }
    public HomeSearchPreferencesDto Preferences { get; set; } = new();
    public int[]? PropertyIds { get; set; }
}