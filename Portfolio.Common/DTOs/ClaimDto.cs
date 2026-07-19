namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// A single identity claim (type/value pair) belonging to a user.
    /// </summary>
    public sealed record ClaimDto
    {
        /// <summary>Claim type identifier (e.g. a URI or short name such as "role").</summary>
        public string Type { get; init; } = null!;

        /// <summary>Value assigned to the claim type (e.g. "admin").</summary>
        public string Value { get; init; } = null!;
    }
}