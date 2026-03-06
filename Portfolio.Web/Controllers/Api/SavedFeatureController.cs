using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for managing saved features.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous] // Uses anonymous user identity from middleware
    public class SavedFeaturesController : ControllerBase
    {
        private readonly ISavedFeatureService _service;

        public SavedFeaturesController(ISavedFeatureService service)
        {
            _service = service;
        }

        /// <summary>
        /// Gets all saved features for the current user.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of saved features.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(List<SavedFeatureDto>), 200)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var items = await _service.GetAllAsync(cancellationToken);
            return Ok(items);
        }

        /// <summary>
        /// Creates a new saved feature for the current user.
        /// </summary>
        /// <param name="dto">Feature data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created feature.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(SavedFeatureDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> Create([FromBody] CreateSavedFeatureDto dto, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dto.LayerId) || string.IsNullOrWhiteSpace(dto.FeatureId))
                return BadRequest(new { error = "LayerId and FeatureId are required." });

            try
            {
                var created = await _service.CreateAsync(dto, cancellationToken);
                return Ok(created);
            }
            catch (InvalidOperationException)
            {
                return Conflict(new { error = "Feature already saved" });
            }
        }

        /// <summary>
        /// Deletes a saved feature by database ID or feature key.
        /// </summary>
        /// <param name="id">Feature database ID or feature key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>No content if deleted, NotFound if not found.</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest();

            if (int.TryParse(id, out var intId))
            {
                var ok = await _service.DeleteByDbIdAsync(intId, cancellationToken);
                if (!ok) return NotFound();
                return NoContent();
            }
            else
            {
                var ok = await _service.DeleteByFeatureKeyAsync(id, cancellationToken);
                if (!ok) return NotFound();
                return NoContent();
            }
        }
    }
}