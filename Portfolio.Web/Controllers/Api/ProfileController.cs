using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for anonymous user profile and claims.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class ProfileController : ControllerBase
    {
        private readonly IUserProfileService _profileService;

        public ProfileController(IUserProfileService profileService)
        {
            _profileService = profileService;
        }

        /// <summary>
        /// Gets the anonymous user profile and claims.
        /// </summary>
        /// <returns>Profile DTO.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(ProfileDto), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ProfileDto>> Get(CancellationToken cancellationToken)
        {
            var userId = _profileService.GetCurrentUserId();
            if (userId == null) return BadRequest(new { error = "Anonymous identity not established." });

            var claims = await _profileService.GetClaimsAsync(cancellationToken);
            var dto = new ProfileDto { UserId = userId.Value, Claims = claims };
            return Ok(dto);
        }

        /// <summary>
        /// Sets or updates a claim for the anonymous user.
        /// </summary>
        /// <param name="body">Claim data.</param>
        /// <param name="cancellationToken">Cancellation token for the request.</param>
        [HttpPost("claim")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SetClaim([FromBody] ClaimCreateDto body, CancellationToken cancellationToken)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.Type)) return BadRequest(new { error = "Type and value required." });

            await _profileService.SetClaimAsync(body.Type, body.Value, cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Removes a claim for the anonymous user.
        /// </summary>
        /// <param name="type">Claim type.</param>
        /// <param name="cancellationToken">Cancellation token for the request.</param>
        [HttpDelete("claim/{type}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> RemoveClaim(string type, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(type)) return BadRequest(new { error = "Type required." });

            var removed = await _profileService.RemoveClaimAsync(type, cancellationToken);
            return removed ? NoContent() : NotFound();
        }
    }
}