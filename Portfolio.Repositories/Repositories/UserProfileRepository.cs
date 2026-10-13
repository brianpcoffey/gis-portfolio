using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portfolio.Common.Models;
using Portfolio.Repositories.Diagnostics;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories
{
    /// <summary>EF Core-backed repository for user profiles and their associated claims.</summary>
    public class UserProfileRepository : IUserProfileRepository
    {
        private readonly PortfolioDbContext _db;
        private readonly ILogger<UserProfileRepository> _logger;

        public UserProfileRepository(PortfolioDbContext db, ILogger<UserProfileRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

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

        public async Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        }

/// <summary>
/// Finds the profile holding the given claim type/value, e.g. to locate a profile by GoogleId without its UserId.
/// </summary>
public async Task<UserProfile?> GetProfileByClaimAsync(string claimType, string claimValue, CancellationToken cancellationToken = default)
{
    var claim = await _db.UserClaims
        .AsNoTracking()
        .Include(c => c.UserProfile)
        .FirstOrDefaultAsync(c => c.ClaimType == claimType && c.ClaimValue == claimValue, cancellationToken);

    return claim?.UserProfile;
}


        // Overwrites every field of an existing profile; persists before returning.
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

        // Returns false if no profile was found.
        public async Task<bool> DeleteProfileAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (profile == null)
            {
                _logger.LogWarning("UserProfile not found for user {UserId} during delete", LogRedaction.UserId(userId));
                return false;
            }
            _db.UserProfiles.Remove(profile);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        // Read-only (AsNoTracking).
        public async Task<List<UserClaim>> GetClaimsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.UserClaims
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .ToListAsync(cancellationToken);
        }

        public async Task<UserClaim?> GetClaimAsync(Guid userId, string type, CancellationToken cancellationToken = default)
        {
            return await _db.UserClaims
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId && c.ClaimType == type, cancellationToken);
        }

        public async Task<bool> HasClaimAsync(Guid userId, string type, CancellationToken cancellationToken = default)
        {
            return await _db.UserClaims
                .AsNoTracking()
                .AnyAsync(c => c.UserId == userId && c.ClaimType == type, cancellationToken);
        }

        public async Task<int> CountClaimsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.UserClaims
                .AsNoTracking()
                .CountAsync(c => c.UserId == userId, cancellationToken);
        }

        // Creates the claim or updates the value of an existing one; persists immediately.
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

        // Returns false if the claim did not exist.
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