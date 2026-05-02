using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Polly.CircuitBreaker;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for querying ArcGIS features.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/features")]
    [AllowAnonymous]
    public class FeaturesController : ControllerBase
    {
        private readonly IArcGisService _arcGisService;
        private readonly ILogger<FeaturesController> _logger;

        public FeaturesController(IArcGisService arcGisService, ILogger<FeaturesController> logger)
        {
            _arcGisService = arcGisService;
            _logger = logger;
        }

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
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFeatures([FromQuery] string layerId, [FromQuery] string? bbox, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(layerId))
                return BadRequest(new { error = "layerId is required." });

            try
            {
                var features = await _arcGisService.QueryFeaturesAsync(layerId, bbox, cancellationToken);
                return Ok(features);
            }
            catch (BrokenCircuitException)
            {
                _logger.LogWarning("ArcGIS feature service circuit breaker is open.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
                {
                    Status = 503,
                    Title  = "Feature Service Unavailable",
                    Detail = "The upstream GIS feature service is temporarily unavailable. Retry after 30 seconds.",
                    Extensions = { ["retryAfterSeconds"] = 30 }
                });
            }
        }
    }
}