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
    /// </summary>
    public class UserProfileService : IUserProfileService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserProfileRepository _repo;
        private readonly TimeProvider _timeProvider;
        private const string HttpContextItemKey = "AnonUserId";

        public UserProfileService(
            IHttpContextAccessor httpContextAccessor,
            IUserProfileRepository repo,
            TimeProvider timeProvider)
        {
            _httpContextAccessor = httpContextAccessor;
            _repo = repo;
            _timeProvider = timeProvider;
        }

        public Guid? GetCurrentUserId()
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx == null) return null;
            if (ctx.Items.TryGetValue(HttpContextItemKey, out var o) && o is Guid g) return g;
            return null;
        }

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

            if (!string.IsNullOrWhiteSpace(dto.DisplayName))
            {
                await _repo.SetClaimAsync(userId, ProfileClaimTypes.DisplayName, dto.DisplayName.Trim(), cancellationToken);
            }
            else
            {
                await _repo.RemoveClaimAsync(userId, ProfileClaimTypes.DisplayName, cancellationToken);
            }

            var profile = await _repo.GetProfileAsync(userId, cancellationToken)
                ?? throw new InvalidOperationException($"Profile not found for {userId}");
            var claims = await _repo.GetClaimsAsync(userId, cancellationToken);
            return ToDto(profile, claims);
        }

        public async Task<bool> DeleteProfileAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _repo.DeleteProfileAsync(userId, cancellationToken);
        }

        public async Task<Guid> CreateOrUpdateFromGoogleAsync(GoogleProfileDto google, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(google);
            if (string.IsNullOrWhiteSpace(google.GoogleId))
                throw new ArgumentException("GoogleId is required", nameof(google));

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var existing = await _repo.GetProfileByClaimAsync(
                ProfileClaimTypes.GoogleId, google.GoogleId, cancellationToken);

            Guid userId;

            if (existing != null)
            {
                userId = existing.UserId;
                existing.LastActiveDate = now;
                await _repo.AddOrUpdateProfileAsync(existing, cancellationToken);
            }
            else
            {
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
                    userId = anonId.Value;
                    var profile = (await _repo.GetProfileAsync(userId, cancellationToken))!;
                    profile.LastActiveDate = now;
                    await _repo.AddOrUpdateProfileAsync(profile, cancellationToken);
                }
                else
                {
                    userId = Guid.NewGuid();
                    await _repo.AddOrUpdateProfileAsync(new UserProfile
                    {
                        UserId = userId,
                        CreatedDate = now,
                        LastActiveDate = now
                    }, cancellationToken);
                }

                await _repo.SetClaimAsync(userId, ProfileClaimTypes.GoogleId, google.GoogleId, cancellationToken);
            }

            await _repo.SetClaimAsync(userId, ProfileClaimTypes.Email, google.Email, cancellationToken);
            await _repo.SetClaimAsync(userId, ProfileClaimTypes.Name, google.Name, cancellationToken);
            if (!string.IsNullOrEmpty(google.Picture))
                await _repo.SetClaimAsync(userId, ProfileClaimTypes.Picture, google.Picture, cancellationToken);

            return userId;
        }

        public async Task<bool> IsGoogleLinkedAsync(CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return false;

            var claim = await _repo.GetClaimAsync(userId.Value, ProfileClaimTypes.GoogleId, cancellationToken);
            return claim != null;
        }

        private static ProfileDto ToDto(UserProfile profile, List<UserClaim> claims) => new()
        {
            UserId = profile.UserId,
            Claims = claims.Select(c => new ClaimDto { Type = c.ClaimType, Value = c.ClaimValue })
        };
    }
}