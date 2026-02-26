namespace Portfolio.Common.DTOs
{
    public sealed record ProfileDto
    {
        public Guid UserId { get; init; }
        public IEnumerable<ClaimDto> Claims { get; init; } = Array.Empty<ClaimDto>();
    }
}