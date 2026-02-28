namespace Portfolio.Common.Models
{
    public class UserProfile
    {
        public Guid UserId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastActiveDate { get; set; }
        public ICollection<UserClaim> Claims { get; set; } = new List<UserClaim>();
    }
}