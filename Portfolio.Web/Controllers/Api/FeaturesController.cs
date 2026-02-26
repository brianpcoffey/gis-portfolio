using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for querying ArcGIS features.
    /// </summary>
    [ApiController]
    [Route("api/features")]
    [AllowAnonymous]
    public class FeaturesController(IArcGisService arcGisService) : ControllerBase
    {
        private readonly IArcGisService _arcGisService = arcGisService;

        /// <summary>
        /// Gets features from a layer, optionally filtered by bounding box.
        /// </summary>
        /// <param name="layerId">Layer identifier.</param>
        /// <param name="bbox">Bounding box filter (optional).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of features.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(List<FeatureDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetFeatures([FromQuery] string layerId, [FromQuery] string? bbox, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(layerId))
                return BadRequest(new { error = "layerId is required." });

            var features = await _arcGisService.QueryFeaturesAsync(layerId, bbox, cancellationToken);
            return Ok(features);
        }
    }
}