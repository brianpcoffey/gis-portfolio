namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// Represents the Google identity fields extracted from OAuth claims.
    /// </summary>
    public sealed record GoogleProfileDto
    {
        public required string GoogleId { get; init; }
        public required string Email { get; init; }
        public required string Name { get; init; }
        public string? Picture { get; init; }
        public Guid? AnonymousUserId { get; set; }
    }
}