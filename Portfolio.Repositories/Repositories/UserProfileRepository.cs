using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories
{
    public class UserProfileRepository : IUserProfileRepository
    {
        private readonly PortfolioDbContext _db;

        public UserProfileRepository(PortfolioDbContext db)
        {
            _db = db;
        }

        public async Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        }

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

        public async Task<bool> DeleteProfileAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (profile == null) return false;
            _db.UserProfiles.Remove(profile);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

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