using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers
{
    [ApiController]
    [Route("api/saved-features")]
    [Authorize]
    public class SavedFeaturesApiController : ControllerBase
    {
        private readonly ISavedFeatureService _savedFeatureService;

        public SavedFeaturesApiController(ISavedFeatureService savedFeatureService)
        {
            _savedFeatureService = savedFeatureService;
        }

        // GET: /api/savedfeatures
        [HttpGet]
        [ProducesResponseType(typeof(List<SavedFeatureDto>), 200)]
        public async Task<IActionResult> GetAll()
        {
            var features = await _savedFeatureService.GetAllAsync();
            return Ok(features);
        }

        // GET: /api/savedfeatures/{id}
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(SavedFeatureDto), 200)]
        [ProducesResponseType(typeof(object), 404)]
        public async Task<IActionResult> GetById(int id)
        {
            var feature = await _savedFeatureService.GetByIdAsync(id);
            return feature is null ? NotFound() : Ok(feature);
        }

        // POST: /api/savedfeatures
        [HttpPost]
        [ProducesResponseType(typeof(SavedFeatureDto), 201)]
        [ProducesResponseType(typeof(object), 400)]
        public async Task<IActionResult> Create(
            [FromBody] SavedFeatureCreateDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _savedFeatureService.CreateAsync(dto, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // PUT: /api/savedfeatures/{id}
        [HttpPut("{id:int}")]
        public async Task<ActionResult<SavedFeatureDto>> Update(int id, [FromBody] SavedFeatureDto dto)
        {
            if (id != dto.Id)
                return BadRequest(new { message = "ID mismatch" });

            try
            {
                var result = await _savedFeatureService.UpdateAsync(dto);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        // DELETE: /api/savedfeatures/{id}
        [HttpDelete("{id:int}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(typeof(object), 404)]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _savedFeatureService.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }
    }
}