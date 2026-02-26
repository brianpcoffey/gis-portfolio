namespace Portfolio.Common.DTOs
{
    public sealed record ClaimCreateDto
    {
        public string Type { get; init; } = null!;
        public string Value { get; init; } = null!;
    }
}