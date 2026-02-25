using Microsoft.AspNetCore.Mvc;
using Portfolio.Services.Interfaces;
using Portfolio.Common.DTOs;

namespace Portfolio.Web.Controllers.Api
{
    [ApiController]
    [Route("api/features")]
    public class FeaturesApiController(IArcGisService arcGisService) : ControllerBase
    {
        private readonly IArcGisService _arcGisService = arcGisService;

        // GET: /api/features?layerId=3&bbox=...
        [HttpGet]
        [ProducesResponseType(typeof(List<FeatureDto>), 200)]
        public async Task<IActionResult> GetFeatures([FromQuery] string layerId, [FromQuery] string? bbox, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(layerId))
                return BadRequest(new { error = "layerId is required." });

            var features = await _arcGisService.QueryFeaturesAsync(layerId, bbox, cancellationToken);
            return Ok(features);
        }
    }
}