namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// Fields the user is allowed to edit on their profile
    /// (Google-sourced fields such as email and picture are refreshed automatically on login).
    /// </summary>
    public sealed record UpdateProfileDto
    {
        /// <summary>User-chosen display name; if null or empty, the Google name is used.</summary>
        public string? DisplayName { get; init; }
    }
}
