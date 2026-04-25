namespace Portfolio.Common.Models;

/// <summary>
/// Persisted user search / selected properties for the Home Finder.
/// </summary>
public class SavedSearch
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;

    // Serialized user preferences (JSON)
    public string PreferencesJson { get; set; } = "{}";

    // Comma-separated top-10 property IDs
    public string TopPropertyIds { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime? LastModified { get; set; }
}