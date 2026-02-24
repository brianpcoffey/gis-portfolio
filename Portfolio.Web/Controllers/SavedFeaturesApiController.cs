using Microsoft.AspNetCore.Mvc;
using Portfolio.Services.Interfaces;
using Portfolio.Common.DTOs;

namespace Portfolio.Web.Controllers
{
    [ApiController]
    [Route("api/savedfeatures")]
    public class SavedFeaturesApiController : ControllerBase
    {
        private readonly ISavedFeatureService _service;

        public SavedFeaturesApiController(ISavedFeatureService service)
        {
            _service = service;
        }

        // GET: /api/savedfeatures
        [HttpGet]
        [ProducesResponseType(typeof(List<SavedFeatureDto>), 200)]
        public async Task<IActionResult> GetAll()
        {
            var features = await _service.GetAllAsync();
            return Ok(features);
        }

        // POST: /api/savedfeatures
        [HttpPost]
        [ProducesResponseType(typeof(SavedFeatureDto), 201)]
        [ProducesResponseType(typeof(object), 400)]
        public async Task<IActionResult> Add([FromBody] SavedFeatureDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var created = await _service.AddAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        // GET: /api/savedfeatures/{id}
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(SavedFeatureDto), 200)]
        [ProducesResponseType(typeof(object), 404)]
        public async Task<IActionResult> GetById(int id)
        {
            var feature = await _service.GetByIdAsync(id);
            if (feature == null)
                return NotFound(new { error = "Not found" });
            return Ok(feature);
        }

        // DELETE: /api/savedfeatures/{id}
        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(typeof(object), 404)]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound(new { error = "Not found" });
            return NoContent();
        }
    }
}