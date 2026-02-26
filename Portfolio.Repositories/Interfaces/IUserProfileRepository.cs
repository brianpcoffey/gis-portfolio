using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces
{
    public interface IUserProfileRepository
    {
        Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<UserProfile> AddOrUpdateProfileAsync(UserProfile profile, CancellationToken cancellationToken = default);
        Task<bool> DeleteProfileAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<List<UserClaim>> GetClaimsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<UserClaim?> GetClaimAsync(Guid userId, string type, CancellationToken cancellationToken = default);
        Task SetClaimAsync(Guid userId, string type, string value, CancellationToken cancellationToken = default);
        Task<bool> RemoveClaimAsync(Guid userId, string type, CancellationToken cancellationToken = default);
    }
}