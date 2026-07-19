namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// Represents the Google identity fields extracted from OAuth claims.
    /// </summary>
    public sealed record GoogleProfileDto
    {
        /// <summary>Stable Google account identifier (the OAuth "sub" claim).</summary>
        public required string GoogleId { get; init; }

        /// <summary>Email address from the Google account.</summary>
        public required string Email { get; init; }

        /// <summary>Full display name from the Google account.</summary>
        public required string Name { get; init; }

        /// <summary>URL of the account's profile picture, if provided.</summary>
        public string? Picture { get; init; }

        /// <summary>Id of the pre-login anonymous user to merge into this account, if any.</summary>
        public Guid? AnonymousUserId { get; set; }
    }
}