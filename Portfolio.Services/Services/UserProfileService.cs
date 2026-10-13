using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Portfolio.Common.Constants;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Diagnostics;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;
using System.Security.Claims;

namespace Portfolio.Services.Services
{
    /// <summary>
    /// Manages user profiles and claims.
    /// Resolves the current user from HttpContext.Items["PortfolioIdentity"] (set by the anonymous-identity middleware or the Google sign-in event).
    /// </summary>
    public class UserProfileService : IUserProfileService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserProfileRepository _repo;
        private readonly ILogger<UserProfileService> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly UserProfileSeedService _seedService;
        private const string HttpContextItemKey = "PortfolioIdentity";
        private const int MaxClaimTypeLength = 200;
        private const int MaxClaimValueLength = 1000;
        private const int MaxClaimsPerUser = 50;

        public UserProfileService(
            IHttpContextAccessor httpContextAccessor,
            IUserProfileRepository repo,
            TimeProvider timeProvider,
            UserProfileSeedService seedService,
            ILogger<UserProfileService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _repo = repo;
            _timeProvider = timeProvider;
            _seedService = seedService;
            _logger = logger;
        }

        // Reads the current user's GUID from HttpContext.Items["PortfolioIdentity"], set by AnonymousUserMiddleware or the sign-in event.
        // Returns null if called outside a valid HTTP request context.
        public Guid? GetCurrentUserId()
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx == null) return null;
            // Prefer authenticated user identity resolved from Items (set during sign-in event).
            // Falls back to anonymous identity set by AnonymousUserMiddleware.
            if (ctx.Items.TryGetValue(HttpContextItemKey, out var o) && o is Guid g) return g;
            return null;
        }

        // Returns all claims for the current anonymous user as ClaimDtos.
        // Throws InvalidOperationException if no user identity is present on the request.
        public async Task<List<ClaimDto>> GetClaimsAsync(CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId() ?? throw new InvalidOperationException("UserId not available");
            var claims = await _repo.GetClaimsAsync(userId, cancellationToken);
            return claims.Select(c => new ClaimDto { Type = c.ClaimType, Value = c.ClaimValue }).ToList();
        }

        // Returns the value of the claim matching the given type for the current user.
        // Returns null if the claim does not exist.
        public async Task<string?> GetClaimAsync(string type, CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId() ?? throw new InvalidOperationException("UserId not available");
            var claim = await _repo.GetClaimAsync(userId, type, cancellationToken);
            return claim?.ClaimValue;
        }

        // Creates or overwrites the claim of the given type for the current user.
        // Throws if the claim type is blank or the value is null, if the type exceeds
        // MaxClaimTypeLength or the value MaxClaimValueLength, or if adding a new claim type
        // would take the user past MaxClaimsPerUser.
        public async Task SetClaimAsync(string type, string value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("Claim type required", nameof(type));
            if (value is null) throw new ArgumentNullException(nameof(value));
            if (type.Length > MaxClaimTypeLength) throw new ArgumentException($"Claim type must be at most {MaxClaimTypeLength} characters", nameof(type));
            if (value.Length > MaxClaimValueLength) throw new ArgumentException($"Claim value must be at most {MaxClaimValueLength} characters", nameof(value));

            var userId = GetCurrentUserId() ?? throw new InvalidOperationException("UserId not available");
            if (!await _repo.HasClaimAsync(userId, type, cancellationToken) &&
                await _repo.CountClaimsAsync(userId, cancellationToken) >= MaxClaimsPerUser)
                throw new ArgumentException($"A profile can hold at most {MaxClaimsPerUser} claims", nameof(type));

            await _repo.SetClaimAsync(userId, type, value, cancellationToken);
            _logger.LogInformation("Claim '{ClaimType}' set for user {UserId}", type, LogRedaction.UserId(userId));
        }

        // Removes the claim of the given type for the current user.
        // Returns false if the claim did not exist.
        public async Task<bool> RemoveClaimAsync(string type, CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId() ?? throw new InvalidOperationException("UserId not available");
            var removed = await _repo.RemoveClaimAsync(userId, type, cancellationToken);
            if (!removed)
                _logger.LogWarning("Claim '{ClaimType}' not found for user {UserId} during remove", type, LogRedaction.UserId(userId));
            return removed;
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
            Guid userId = Guid.Empty;

            // Atomic: the profile upsert and claim writes commit together, so a
            // mid-sequence failure never leaves a half-provisioned account.
            await _repo.RunInTransactionAsync(async ct =>
            {
                var existing = await _repo.GetProfileByClaimAsync(
                    ProfileClaimTypes.GoogleId, google.GoogleId, ct);

                if (existing != null)
                {
                    userId = existing.UserId;
                    existing.LastActiveDate = now;
                    await _repo.AddOrUpdateProfileAsync(existing, ct);
                }
                else
                {
                    var ctx = _httpContextAccessor.HttpContext;
                    Guid? anonId = null;
                    if (ctx != null &&
                        ctx.Request.Cookies.TryGetValue("AnonUserId", out var cookieVal) &&
                        Guid.TryParse(cookieVal, out var parsed))
                    {
                        // Only an anonymous (never Google-linked) profile may be adopted; a cookie
                        // naming a linked profile must not let this sign-in take it over.
                        var anonProfile = await _repo.GetProfileAsync(parsed, ct);
                        if (anonProfile != null && !await _repo.HasClaimAsync(anonProfile.UserId, ProfileClaimTypes.GoogleId, ct))
                            anonId = anonProfile.UserId;
                    }

                    if (anonId.HasValue)
                    {
                        userId = anonId.Value;
                        var profile = (await _repo.GetProfileAsync(userId, ct))!;
                        profile.LastActiveDate = now;
                        await _repo.AddOrUpdateProfileAsync(profile, ct);
                    }
                    else
                    {
                        userId = Guid.NewGuid();
                        await _repo.AddOrUpdateProfileAsync(new UserProfile
                        {
                            UserId = userId,
                            CreatedDate = now,
                            LastActiveDate = now
                        }, ct);
                    }

                    await _repo.SetClaimAsync(userId, ProfileClaimTypes.GoogleId, google.GoogleId, ct);
                }

                await _repo.SetClaimAsync(userId, ProfileClaimTypes.Email, google.Email, ct);
                await _repo.SetClaimAsync(userId, ProfileClaimTypes.Name, google.Name, ct);
                if (!string.IsNullOrEmpty(google.Picture))
                    await _repo.SetClaimAsync(userId, ProfileClaimTypes.Picture, google.Picture, ct);
            }, cancellationToken);

            // Visitors are seeded at sign-in, not first visit. SeedForUserAsync is idempotent and
            // opens its own transaction on relational providers, so it runs after ours commits,
            // for new, adopted and returning profiles alike (a returning one may have missed an
            // earlier seed). A seed failure must not block the sign-in.
            await TrySeedAsync(userId, cancellationToken);

            return userId;
        }

        private async Task TrySeedAsync(Guid userId, CancellationToken cancellationToken)
        {
            try
            {
                await _seedService.SeedForUserAsync(userId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Seeding sample data failed for user {UserId}; sign-in continues", LogRedaction.UserId(userId));
            }
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