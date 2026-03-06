using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for managing user profiles.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class UserProfileController : ControllerBase
    {
        private readonly IUserProfileService _profileService;

        public UserProfileController(IUserProfileService profileService)
        {
            _profileService = profileService;
        }

        /// <summary>
        /// Gets the current user's profile and claims.
        /// </summary>
        [HttpGet("me")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ProfileDto), 200)]
        public async Task<IActionResult> GetCurrentProfile(CancellationToken cancellationToken)
        {
            try
            {
                var profile = await _profileService.GetCurrentProfileAsync(cancellationToken);
                return Ok(profile);
            }
            catch (InvalidOperationException)
            {
                return NotFound(new { error = "Profile not found" });
            }
        }

        /// <summary>
        /// Updates editable fields on the current user's profile.
        /// </summary>
        [HttpPut("me")]
        [Authorize(Policy = "Authenticated")]
        [ProducesResponseType(typeof(ProfileDto), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> UpdateCurrentProfile([FromBody] UpdateProfileDto dto, CancellationToken cancellationToken)
        {
            var profile = await _profileService.UpdateCurrentProfileAsync(dto, cancellationToken);
            return Ok(profile);
        }

        /// <summary>
        /// Gets a user profile by internal ID.
        /// </summary>
        /// <param name="userId">The internal GUID user ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("{userId:guid}")]
        [Authorize(Policy = "Authenticated")]
        [ProducesResponseType(typeof(ProfileDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetById(Guid userId, CancellationToken cancellationToken)
        {
            var profile = await _profileService.GetProfileByIdAsync(userId, cancellationToken);
            if (profile == null) return NotFound();
            return Ok(profile);
        }

        /// <summary>
        /// Gets a user profile by Google ID.
        /// </summary>
        /// <param name="googleId">The Google subject identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("google/{googleId}")]
        [Authorize(Policy = "Authenticated")]
        [ProducesResponseType(typeof(ProfileDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetByGoogleId(string googleId, CancellationToken cancellationToken)
        {
            var profile = await _profileService.GetProfileByGoogleIdAsync(googleId, cancellationToken);
            if (profile == null) return NotFound();
            return Ok(profile);
        }

        /// <summary>
        /// Gets all claims for the current user.
        /// </summary>
        [HttpGet("me/claims")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<ClaimDto>), 200)]
        public async Task<IActionResult> GetClaims(CancellationToken cancellationToken)
        {
            var claims = await _profileService.GetClaimsAsync(cancellationToken);
            return Ok(claims);
        }

        /// <summary>
        /// Sets a claim for the current user.
        /// </summary>
        [HttpPut("me/claims")]
        [Authorize(Policy = "Authenticated")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SetClaim([FromBody] ClaimDto dto, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dto.Type) || dto.Value is null)
                return BadRequest(new { error = "Type and Value are required." });

            await _profileService.SetClaimAsync(dto.Type, dto.Value, cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Removes a claim for the current user.
        /// </summary>
        /// <param name="type">The claim type to remove.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpDelete("me/claims/{type}")]
        [Authorize(Policy = "Authenticated")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> RemoveClaim(string type, CancellationToken cancellationToken)
        {
            var removed = await _profileService.RemoveClaimAsync(type, cancellationToken);
            return removed ? NoContent() : NotFound();
        }

        /// <summary>
        /// Deletes a user profile and all associated claims.
        /// </summary>
        /// <param name="userId">The internal GUID user ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpDelete("{userId:guid}")]
        [Authorize(Policy = "Authenticated")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Delete(Guid userId, CancellationToken cancellationToken)
        {
            var deleted = await _profileService.DeleteProfileAsync(userId, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
    }
}