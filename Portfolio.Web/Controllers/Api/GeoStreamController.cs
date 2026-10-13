using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Filters;
using Portfolio.Web.RateLimiting;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoint for batch GPS telemetry processing — spatial grid aggregation,
    /// speed metrics, and high-speed anomaly detection.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [EnableRateLimiting("compute")]
    [CpuBound]
    [Route("api/v{version:apiVersion}/geostream")]
    public class GeoStreamController : ControllerBase
    {
        private readonly IGeoStreamProcessorService _geoStreamProcessorService;

        public GeoStreamController(IGeoStreamProcessorService geoStreamProcessorService)
        {
            _geoStreamProcessorService = geoStreamProcessorService;
        }

        /// <summary>Processes a batch of telemetry events into grid aggregates and anomaly counts.</summary>
        /// <param name="request">Telemetry batch and processing options.</param>
        /// <returns>Aggregated telemetry processing result.</returns>
        [HttpPost("events")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(GeoStreamBatchResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(2_000_000)]
        public async Task<ActionResult<GeoStreamBatchResultDto>> ProcessEvents(
            [FromBody] GeoStreamBatchRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _geoStreamProcessorService.ProcessBatchAsync(request, cancellationToken));
        }
    }
}
