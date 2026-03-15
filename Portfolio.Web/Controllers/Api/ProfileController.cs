using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.Constants;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for anonymous and authenticated user profile claims.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ProfileController : ControllerBase
    {
        private readonly IUserProfileService _profileService;

        // Claim types that cannot be set or removed by anonymous users or manually by authenticated users
        private static readonly HashSet<string> ProtectedClaimTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ProfileClaimTypes.GoogleId,
            ProfileClaimTypes.Email,
            ProfileClaimTypes.Name,
            ProfileClaimTypes.Picture
        };

        public ProfileController(IUserProfileService profileService)
        {
            _profileService = profileService;
        }

        #region Helper Methods
        private bool IsInvalidClaim(ClaimCreateDto claim) =>
            claim == null || string.IsNullOrWhiteSpace(claim.Type);
        #endregion

        /// <summary>
        /// Gets the anonymous user profile and claims.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ProfileDto), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ProfileDto>> Get(CancellationToken cancellationToken)
        {
            var userId = _profileService.GetCurrentUserId();
            if (userId == null)
                return BadRequest(new { error = "Anonymous identity not established." });

            var claims = await _profileService.GetClaimsAsync(cancellationToken);
            return Ok(new ProfileDto { UserId = userId.Value, Claims = claims });
        }

        /// <summary>
        /// Sets or updates a claim for the anonymous user.
        /// </summary>
        [HttpPost("claims")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ClaimDto), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SetClaim([FromBody] ClaimCreateDto claim, CancellationToken cancellationToken)
        {
            if (IsInvalidClaim(claim))
                return BadRequest(new { error = "Type and value required." });

            if (ProtectedClaimTypes.Contains(claim.Type))
            {
                // log attempt here if needed
                return BadRequest(new { error = $"Claim type '{claim.Type}' cannot be set anonymously." });
            }

            await _profileService.SetClaimAsync(claim.Type, claim.Value, cancellationToken);
            return Ok(new ClaimDto { Type = claim.Type, Value = claim.Value });
        }

        /// <summary>
        /// Removes a claim for the anonymous user.
        /// </summary>
        [HttpDelete("claims/{type}")]
        [AllowAnonymous]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> RemoveClaim(string type, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(type))
                return BadRequest(new { error = "Type required." });

            if (ProtectedClaimTypes.Contains(type))
            {
                // log attempt here if needed
                return BadRequest(new { error = $"Claim type '{type}' cannot be removed anonymously." });
            }

            var removed = await _profileService.RemoveClaimAsync(type, cancellationToken);
            return removed ? NoContent() : NotFound();
        }

        /// <summary>
        /// Sets or updates a claim for the authenticated user.
        /// </summary>
        [HttpPut("me/claims")]
        [Authorize(Policy = "Authenticated")]
        [ProducesResponseType(typeof(ClaimDto), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SetAuthenticatedClaim([FromBody] ClaimCreateDto claim, CancellationToken cancellationToken)
        {
            if (IsInvalidClaim(claim))
                return BadRequest(new { error = "Type and value required." });

            if (ProtectedClaimTypes.Contains(claim.Type))
            {
                // log attempt here if needed
                return BadRequest(new { error = $"Claim type '{claim.Type}' is managed by Google OAuth and cannot be set manually." });
            }

            await _profileService.SetClaimAsync(claim.Type, claim.Value, cancellationToken);
            return Ok(claim);
        }
    }
}