using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SavedFeaturesController : ControllerBase
    {
        private readonly ISavedFeatureService _service;

        public SavedFeaturesController(ISavedFeatureService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _service.GetAllAsync();
            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSavedFeatureDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.LayerId) || string.IsNullOrWhiteSpace(dto.FeatureId))
                return BadRequest(new { error = "LayerId and FeatureId are required." });

            try
            {
                var created = await _service.CreateAsync(dto);
                return Ok(created);
            }
            catch (InvalidOperationException)
            {
                return Conflict(new { error = "Feature already saved" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest();

            if (int.TryParse(id, out var intId))
            {
                var ok = await _service.DeleteByDbIdAsync(intId);
                if (!ok) return NotFound();
                return NoContent();
            }
            else
            {
                var ok = await _service.DeleteByFeatureKeyAsync(id);
                if (!ok) return NotFound();
                return NoContent();
            }
        }
    }
}