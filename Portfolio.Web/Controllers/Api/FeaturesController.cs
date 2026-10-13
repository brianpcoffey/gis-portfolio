using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    [EnableRateLimiting("expensive")] // proxies the ArcGIS feature service
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

            // The id is spliced into an upstream URL path, so accept only a short numeric layer id
            // (dot-segments such as ".." would otherwise be normalised away by the URI parser).
            if (!IsValidLayerId(layerId))
                return BadRequest(new { error = "layerId must be 1 to 3 digits." });

            if (!IsValidBbox(bbox))
                return BadRequest(new { error = "bbox must be four comma-separated numbers: minX,minY,maxX,maxY." });

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

        // ASCII digits only, and \z (not $) so a trailing newline does not slip through.
        private static bool IsValidLayerId(string layerId) =>
            System.Text.RegularExpressions.Regex.IsMatch(layerId, @"^[0-9]{1,3}\z");

        // null or empty means "no bbox filter". Otherwise exactly four finite numbers.
        private static bool IsValidBbox(string? bbox)
        {
            if (string.IsNullOrEmpty(bbox))
                return true;

            var parts = bbox.Split(',');
            return parts.Length == 4 && parts.All(p =>
                double.TryParse(p, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) && double.IsFinite(v));
        }
    }
}