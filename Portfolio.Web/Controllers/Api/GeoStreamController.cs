using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoint for batch GPS telemetry processing — spatial grid aggregation,
    /// speed metrics, and high-speed anomaly detection.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [Route("api/v{version:apiVersion}/geostream")]
    public class GeoStreamController : ControllerBase
    {
        private readonly IGeoStreamProcessorService _geoStreamProcessorService;
        private readonly ILogger<GeoStreamController> _logger;

        public GeoStreamController(
            IGeoStreamProcessorService geoStreamProcessorService,
            ILogger<GeoStreamController> logger)
        {
            _geoStreamProcessorService = geoStreamProcessorService;
            _logger = logger;
        }

        /// <summary>
        /// Processes a batch of telemetry events into grid aggregates and anomaly counts.
        /// </summary>
        /// <param name="request">Telemetry batch and processing options.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Aggregated telemetry processing result.</returns>
        [HttpPost("events")]
        [ProducesResponseType(typeof(GeoStreamBatchResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(2_000_000)]
        public async Task<ActionResult<GeoStreamBatchResultDto>> ProcessEvents(
            [FromBody] GeoStreamBatchRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _geoStreamProcessorService.ProcessBatchAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid geostream telemetry batch request.");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
