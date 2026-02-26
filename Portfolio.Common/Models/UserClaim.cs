namespace Portfolio.Common.Models
{
    public class UserClaim
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }

        public string ClaimType { get; set; } = null!;
        public string ClaimValue { get; set; } = null!;

        public UserProfile? UserProfile { get; set; }
    }
}