using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces
{
    public interface IUserProfileRepository
    {
        Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<UserProfile?> GetProfileByClaimAsync(string claimType, string claimValue, CancellationToken cancellationToken = default);
        Task<UserProfile> AddOrUpdateProfileAsync(UserProfile profile, CancellationToken cancellationToken = default);
        Task<bool> DeleteProfileAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<List<UserClaim>> GetClaimsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<UserClaim?> GetClaimAsync(Guid userId, string type, CancellationToken cancellationToken = default);
        Task SetClaimAsync(Guid userId, string type, string value, CancellationToken cancellationToken = default);
        Task<bool> RemoveClaimAsync(Guid userId, string type, CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs <paramref name="action"/> inside a single database transaction, committing on
        /// success and rolling back if it throws — so multi-write sequences apply atomically.
        /// </summary>
        Task RunInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
    }
}