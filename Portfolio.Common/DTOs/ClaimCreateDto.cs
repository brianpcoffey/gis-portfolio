namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// Request body for adding a single identity claim (type/value pair) to a user.
    /// </summary>
    public sealed record ClaimCreateDto
    {
        /// <summary>Claim type identifier (e.g. a URI or short name such as "role").</summary>
        public string Type { get; init; } = null!;

        /// <summary>Value assigned to the claim type (e.g. "admin").</summary>
        public string Value { get; init; } = null!;
    }
}