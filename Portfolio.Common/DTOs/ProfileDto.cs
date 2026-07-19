namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// A user's profile: their unique id together with all identity claims.
    /// </summary>
    public sealed record ProfileDto
    {
        /// <summary>Unique identifier of the user this profile belongs to.</summary>
        public Guid UserId { get; init; }

        /// <summary>All identity claims associated with the user.</summary>
        public IEnumerable<ClaimDto> Claims { get; init; } = Array.Empty<ClaimDto>();
    }
}