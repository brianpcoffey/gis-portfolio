using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services
{
    /// <summary>
    /// Only uses the UserId present in HttpContext.Items["AnonUserId"].
    /// Never accepts user-supplied UserId.
    /// </summary>
    public class UserProfileService : IUserProfileService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserProfileRepository _repo;
        private readonly ILogger<UserProfileService> _logger;
        private const string HttpContextItemKey = "AnonUserId";

        public UserProfileService(IHttpContextAccessor httpContextAccessor, IUserProfileRepository repo, ILogger<UserProfileService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _repo = repo;
            _logger = logger;
        }

        // Reads the anonymous user's GUID from HttpContext.Items, set by AnonymousUserMiddleware.
        // Returns null if called outside a valid HTTP request context.
        public Guid? GetCurrentUserId()
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx == null) return null;
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
        // Throws if the claim type is blank or the value is null.
        public async Task SetClaimAsync(string type, string value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("Claim type required", nameof(type));
            if (value is null) throw new ArgumentNullException(nameof(value));

            var userId = GetCurrentUserId() ?? throw new InvalidOperationException("UserId not available");
            await _repo.SetClaimAsync(userId, type, value, cancellationToken);
            _logger.LogInformation("Claim '{ClaimType}' set for user {UserId}", type, userId);
        }

        // Removes the claim of the given type for the current user.
        // Returns false if the claim did not exist.
        public async Task<bool> RemoveClaimAsync(string type, CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId() ?? throw new InvalidOperationException("UserId not available");
            var removed = await _repo.RemoveClaimAsync(userId, type, cancellationToken);
            if (!removed)
                _logger.LogWarning("Claim '{ClaimType}' not found for user {UserId} during remove", type, userId);
            return removed;
        }
    }
}