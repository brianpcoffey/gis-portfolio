using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for fleet route optimization: capacitated vehicle routing with time
    /// windows (CVRPTW) solved over the real Redlands road network.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [EnableRateLimiting("expensive")]
    [Route("api/v{version:apiVersion}/fleet")]
    public class FleetRoutingController : ControllerBase
    {
        private readonly IFleetRoutingService _fleetRoutingService;
        private readonly ILogger<FleetRoutingController> _logger;

        public FleetRoutingController(
            IFleetRoutingService fleetRoutingService,
            ILogger<FleetRoutingController> logger)
        {
            _fleetRoutingService = fleetRoutingService;
            _logger = logger;
        }

        /// <summary>
        /// Returns a named demo delivery scenario: depot, customer stops with demands and
        /// time windows, and the fleet available to cover them.
        /// </summary>
        /// <param name="preset">Scenario key: "morning", "fullday", or "tight".</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>The scenario, ready to post straight back to the optimize endpoint.</returns>
        [HttpGet("scenario")]
        [ProducesResponseType(typeof(FleetScenarioDto), 200)]
        [ProducesResponseType(400)]
        // No VaryByQueryKeys: that requires the response-caching middleware, which this app
        // does not register. The header alone is enough — caches key on the full URL, and the
        // preset lives in the query string.
        [ResponseCache(Duration = 3600)]
        public async Task<ActionResult<FleetScenarioDto>> Scenario(
            [FromQuery] string preset,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _fleetRoutingService.GetScenarioAsync(preset ?? string.Empty, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid fleet scenario request.");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Solves the capacitated vehicle routing problem with time windows for the supplied
        /// scenario, returning one route per vehicle used with road-following geometry, the
        /// per-stop arrival schedule, and the convergence trace.
        /// </summary>
        /// <param name="request">Depot, stops, fleet parameters, and search budget.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Routes, unserved stops, objective trace, and honest timings.</returns>
        [HttpPost("optimize")]
        [ProducesResponseType(typeof(OptimizeResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<OptimizeResultDto>> Optimize(
            [FromBody] OptimizeRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _fleetRoutingService.OptimizeAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid fleet optimization request.");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
