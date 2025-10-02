using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers
{
    [Route("api/gis/features")]
    [ApiController]
    public class GisFeaturesController : ControllerBase
    {
        private readonly IGisFeatureService _service;
        private readonly ILogger<GisFeaturesController> _logger;
        public GisFeaturesController(IGisFeatureService service, ILogger<GisFeaturesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Gets all GIS features.
        /// </summary>
        /// <returns>List of GIS features.</returns>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                return Ok(await _service.GetAllAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting GIS features");
                return BadRequest("An error occurred while processing your request.");
            }
        }

        /// <summary>
        /// Adds a new GIS feature.
        /// </summary>
        /// <param name="dto">GIS feature data.</param>
        /// <returns>The created GIS feature.</returns>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] GisFeatureDto dto)
        {
            try
            {
                var userName = User.Identity?.Name ?? "demo";
                var result = await _service.AddAsync(dto, userName);
                return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding GIS feature");
                return BadRequest("An error occurred while processing your request.");
            }
        }

        /// <summary>
        /// Updates an existing GIS feature.
        /// </summary>
        /// <param name="id">Feature ID.</param>
        /// <param name="dto">Updated feature data.</param>
        /// <returns>The updated GIS feature.</returns>
        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] GisFeatureDto dto)
        {
            try
            {
                var userName = User.Identity?.Name ?? "demo";
                dto.Id = id;
                var result = await _service.UpdateAsync(dto, userName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating GIS feature with id {id}");
                return BadRequest("An error occurred while processing your request.");
            }
        }

        /// <summary>
        /// Deletes a GIS feature.
        /// </summary>
        /// <param name="id">Feature ID.</param>
        /// <returns>No content if successful, NotFound if not.</returns>
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userName = User.Identity?.Name ?? "demo";
                var success = await _service.DeleteAsync(id, userName);
                return success ? NoContent() : NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting GIS feature with id {id}");
                return BadRequest("An error occurred while processing your request.");
            }
        }
    }
}
