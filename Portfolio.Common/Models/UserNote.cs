using Portfolio.Common.Models;

public class UserNote
{
    public int Id { get; set; }
    public int SavedFeatureId { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public SavedFeature SavedFeature { get; set; } = null!;
}