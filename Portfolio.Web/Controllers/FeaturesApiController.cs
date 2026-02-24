using Microsoft.AspNetCore.Mvc;
using Portfolio.Services.Interfaces;
using Portfolio.Common.DTOs;

namespace Portfolio.Web.Controllers
{
    [ApiController]
    [Route("api/features")]
    public class FeaturesApiController : ControllerBase
    {
        private readonly IArcGisService _arcGisService;

        public FeaturesApiController(IArcGisService arcGisService)
        {
            _arcGisService = arcGisService;
        }

        // GET: /api/features?layerId=3&bbox=...
        [HttpGet]
        [ProducesResponseType(typeof(List<FeatureDto>), 200)]
        public async Task<IActionResult> GetFeatures([FromQuery] string layerId, [FromQuery] string? bbox)
        {
            if (string.IsNullOrWhiteSpace(layerId))
                return BadRequest(new { error = "layerId is required." });

            var features = await _arcGisService.QueryFeaturesAsync(layerId, bbox);
            return Ok(features);
        }
    }
}