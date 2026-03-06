using Microsoft.AspNetCore.Http;
using Portfolio.Common.Constants;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services
{
    /// <summary>
    /// Manages user profiles and claims.
    /// Resolves the current user from HttpContext.Items["AnonUserId"] (set by middleware).
    /// Never accepts user-supplied UserId on mutating operations scoped to "current user".
    /// </summary>
    public class UserProfileService : IUserProfileService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserProfileRepository _repo;
        private const string HttpContextItemKey = "AnonUserId";

        public UserProfileService(IHttpContextAccessor httpContextAccessor, IUserProfileRepository repo)
        {
            _httpContextAccessor = httpContextAccessor;
            _repo = repo;
        }

        // ---------------------------------------------------------------
        // Identity resolution
        // ---------------------------------------------------------------

        public Guid? GetCurrentUserId()
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx == null) return null;
            if (ctx.Items.TryGetValue(HttpContextItemKey, out var o) && o is Guid g) return g;
            return null;
        }

        // ---------------------------------------------------------------
        // Claims CRUD (scoped to current user)
        // ---------------------------------------------------------------

        public async Task<List<ClaimDto>> GetClaimsAsync(CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId() ?? throw new InvalidOperationException("UserId not available");
            var claims = await _repo.GetClaimsAsync(userId, cancellationToken);
            return claims.Select(c => new ClaimDto { Type = c.ClaimType, Value = c.ClaimValue }).ToList();
        }

        public async Task<string?> GetClaimAsync(string type, CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId() ?? throw new InvalidOperationException("UserId not available");
            var claim = await _repo.GetClaimAsync(userId, type, cancellationToken);
            return claim?.ClaimValue;
        }

        public async Task SetClaimAsync(string type, string value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("Claim type required", nameof(type));
            if (value is null) throw new ArgumentNullException(nameof(value));

            var userId = GetCurrentUserId() ?? throw new InvalidOperationException("UserId not available");
            await _repo.SetClaimAsync(userId, type, value, cancellationToken);
        }

        public async Task<bool> RemoveClaimAsync(string type, CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId() ?? throw new InvalidOperationException("UserId not available");
            return await _repo.RemoveClaimAsync(userId, type, cancellationToken);
        }

        // ---------------------------------------------------------------
        // Profile CRUD
        // ---------------------------------------------------------------

        public async Task<ProfileDto?> GetProfileByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var profile = await _repo.GetProfileAsync(userId, cancellationToken);
            if (profile == null) return null;

            var claims = await _repo.GetClaimsAsync(userId, cancellationToken);
            return ToDto(profile, claims);
        }

        public async Task<ProfileDto?> GetProfileByGoogleIdAsync(string googleId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(googleId)) throw new ArgumentException("GoogleId required", nameof(googleId));

            var profile = await _repo.GetProfileByClaimAsync(ProfileClaimTypes.GoogleId, googleId, cancellationToken);
            if (profile == null) return null;

            var claims = await _repo.GetClaimsAsync(profile.UserId, cancellationToken);
            return ToDto(profile, claims);
        }

        public async Task<ProfileDto> GetCurrentProfileAsync(CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId() ?? throw new InvalidOperationException("UserId not available");
            var profile = await _repo.GetProfileAsync(userId, cancellationToken)
                ?? throw new InvalidOperationException($"Profile not found for {userId}");

            var claims = await _repo.GetClaimsAsync(userId, cancellationToken);
            return ToDto(profile, claims);
        }

        public async Task<ProfileDto> UpdateCurrentProfileAsync(UpdateProfileDto dto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            var userId = GetCurrentUserId() ?? throw new InvalidOperationException("UserId not available");

            // Update the editable display_name claim
            if (!string.IsNullOrWhiteSpace(dto.DisplayName))
            {
                await _repo.SetClaimAsync(userId, ProfileClaimTypes.DisplayName, dto.DisplayName.Trim(), cancellationToken);
            }
            else
            {
                // Clear custom display name — will fall back to Google name
                await _repo.RemoveClaimAsync(userId, ProfileClaimTypes.DisplayName, cancellationToken);
            }

            // Return the updated profile
            var profile = await _repo.GetProfileAsync(userId, cancellationToken)
                ?? throw new InvalidOperationException($"Profile not found for {userId}");
            var claims = await _repo.GetClaimsAsync(userId, cancellationToken);
            return ToDto(profile, claims);
        }

        public async Task<bool> DeleteProfileAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _repo.DeleteProfileAsync(userId, cancellationToken);
        }

        // ---------------------------------------------------------------
        // Google OAuth integration
        // ---------------------------------------------------------------

        public async Task<Guid> CreateOrUpdateFromGoogleAsync(GoogleProfileDto google, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(google);
            if (string.IsNullOrWhiteSpace(google.GoogleId))
                throw new ArgumentException("GoogleId is required", nameof(google));

            var existing = await _repo.GetProfileByClaimAsync(
                ProfileClaimTypes.GoogleId, google.GoogleId, cancellationToken);

            Guid userId;

            if (existing != null)
            {
                // Returning user — update timestamps and refresh claims
                userId = existing.UserId;
                existing.LastActiveDate = DateTime.UtcNow;
                await _repo.AddOrUpdateProfileAsync(existing, cancellationToken);
            }
            else
            {
                // New user — check if there's an anonymous profile to promote
                var ctx = _httpContextAccessor.HttpContext;
                Guid? anonId = null;
                if (ctx != null &&
                    ctx.Request.Cookies.TryGetValue("AnonUserId", out var cookieVal) &&
                    Guid.TryParse(cookieVal, out var parsed))
                {
                    var anonProfile = await _repo.GetProfileAsync(parsed, cancellationToken);
                    if (anonProfile != null)
                        anonId = anonProfile.UserId;
                }

                if (anonId.HasValue)
                {
                    // Promote anonymous profile to Google-linked
                    userId = anonId.Value;
                    var profile = (await _repo.GetProfileAsync(userId, cancellationToken))!;
                    profile.LastActiveDate = DateTime.UtcNow;
                    await _repo.AddOrUpdateProfileAsync(profile, cancellationToken);
                }
                else
                {
                    // Create brand new profile
                    userId = Guid.NewGuid();
                    await _repo.AddOrUpdateProfileAsync(new UserProfile
                    {
                        UserId = userId,
                        CreatedDate = DateTime.UtcNow,
                        LastActiveDate = DateTime.UtcNow
                    }, cancellationToken);
                }

                // Set the GoogleId claim (only on first link)
                await _repo.SetClaimAsync(userId, ProfileClaimTypes.GoogleId, google.GoogleId, cancellationToken);
            }

            // 2. Always refresh mutable Google profile data
            await _repo.SetClaimAsync(userId, ProfileClaimTypes.Email, google.Email, cancellationToken);
            await _repo.SetClaimAsync(userId, ProfileClaimTypes.Name, google.Name, cancellationToken);
            if (!string.IsNullOrEmpty(google.Picture))
                await _repo.SetClaimAsync(userId, ProfileClaimTypes.Picture, google.Picture, cancellationToken);

            return userId;
        }

        // ---------------------------------------------------------------
        // Convenience helpers
        // ---------------------------------------------------------------

        public async Task<bool> IsGoogleLinkedAsync(CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return false;

            var claim = await _repo.GetClaimAsync(userId.Value, ProfileClaimTypes.GoogleId, cancellationToken);
            return claim != null;
        }

        // ---------------------------------------------------------------
        // Mapping
        // ---------------------------------------------------------------

        private static ProfileDto ToDto(UserProfile profile, List<UserClaim> claims)
        {
            return new ProfileDto
            {
                UserId = profile.UserId,
                Claims = claims.Select(c => new ClaimDto { Type = c.ClaimType, Value = c.ClaimValue })
            };
        }
    }
}