using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories
{
    /// <summary>
    /// EF Core-backed repository for user profiles and their associated claims.
    /// </summary>
    public class UserProfileRepository : IUserProfileRepository
    {
        private readonly PortfolioDbContext _db;
        private readonly ILogger<UserProfileRepository> _logger;

        public UserProfileRepository(PortfolioDbContext db, ILogger<UserProfileRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        // Runs the given work inside a single transaction (commit on success, rollback on throw).
        // Uses the provider's execution strategy so it is safe under connection-resiliency config.
        public async Task RunInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    await action(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                }
                catch
                {
                    await tx.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }

        // Retrieves the user profile for the given userId.
        // Returns null if no matching profile exists.
        public async Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        }

// Inserts a new user profile or overwrites all fields of the existing one.
// Persists changes to the database before returning.

/// <summary>
/// Finds a UserProfile that has a specific claim type/value pair.
/// Used to locate profiles by GoogleId without knowing the internal UserId.
/// </summary>
public async Task<UserProfile?> GetProfileByClaimAsync(string claimType, string claimValue, CancellationToken cancellationToken = default)
{
    var claim = await _db.UserClaims
        .AsNoTracking()
        .Include(c => c.UserProfile)
        .FirstOrDefaultAsync(c => c.ClaimType == claimType && c.ClaimValue == claimValue, cancellationToken);

    return claim?.UserProfile;
}


        public async Task<UserProfile> AddOrUpdateProfileAsync(UserProfile profile, CancellationToken cancellationToken = default)
        {
            var existing = await _db.UserProfiles.FirstOrDefaultAsync(u => u.UserId == profile.UserId, cancellationToken);
            if (existing == null)
            {
                _db.UserProfiles.Add(profile);
            }
            else
            {
                _db.Entry(existing).CurrentValues.SetValues(profile);
            }
            await _db.SaveChangesAsync(cancellationToken);
            return profile;
        }

        // Removes the user profile for the given userId.
        // Returns false if no profile was found; true after successful deletion.
        public async Task<bool> DeleteProfileAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (profile == null)
            {
                _logger.LogWarning("UserProfile not found for user {UserId} during delete", userId);
                return false;
            }
            _db.UserProfiles.Remove(profile);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        // Returns all claims stored for the given user. Read-only (AsNoTracking).
        public async Task<List<UserClaim>> GetClaimsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.UserClaims
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .ToListAsync(cancellationToken);
        }

        // Retrieves a single claim by type for the given user.
        // Returns null if the claim does not exist.
        public async Task<UserClaim?> GetClaimAsync(Guid userId, string type, CancellationToken cancellationToken = default)
        {
            return await _db.UserClaims
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId && c.ClaimType == type, cancellationToken);
        }

        // Creates a new claim or updates the value of an existing one for the given user.
        // Persists the change to the database immediately.
        public async Task SetClaimAsync(Guid userId, string type, string value, CancellationToken cancellationToken = default)
        {
            var existing = await _db.UserClaims.FirstOrDefaultAsync(c => c.UserId == userId && c.ClaimType == type, cancellationToken);
            if (existing != null)
            {
                existing.ClaimValue = value;
            }
            else
            {
                _db.UserClaims.Add(new UserClaim
                {
                    UserId = userId,
                    ClaimType = type,
                    ClaimValue = value
                });
            }
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Deletes the claim of the specified type for the given user.
        // Returns false if the claim did not exist; true after successful removal.
        public async Task<bool> RemoveClaimAsync(Guid userId, string type, CancellationToken cancellationToken = default)
        {
            var existing = await _db.UserClaims.FirstOrDefaultAsync(c => c.UserId == userId && c.ClaimType == type, cancellationToken);
            if (existing == null) return false;
            _db.UserClaims.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}