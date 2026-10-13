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
    /// API endpoints for fleet route optimization: capacitated vehicle routing with time
    /// windows (CVRPTW) solved over the real Redlands road network.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [EnableRateLimiting("expensive")]
    [CpuBound]
    [Route("api/v{version:apiVersion}/fleet")]
    public class FleetRoutingController : ControllerBase
    {
        private readonly IFleetRoutingService _fleetRoutingService;

        public FleetRoutingController(IFleetRoutingService fleetRoutingService)
        {
            _fleetRoutingService = fleetRoutingService;
        }

        /// <summary>Returns a named demo delivery scenario.</summary>
        /// <remarks>
        /// The scenario comprises the depot, customer stops with demands and time windows, and the fleet available to
        /// cover them.
        /// </remarks>
        /// <param name="preset">Scenario key: "morning", "fullday", or "tight".</param>
        /// <returns>The scenario, ready to post straight back to the optimize endpoint.</returns>
        [HttpGet("scenario")]
        [BadRequestOnArgumentException]
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
            return Ok(await _fleetRoutingService.GetScenarioAsync(preset ?? string.Empty, cancellationToken));
        }

        /// <summary>Solves the capacitated vehicle routing problem with time windows for a supplied scenario.</summary>
        /// <remarks>
        /// Returns one route per vehicle used with road-following geometry, the per-stop arrival schedule, and the
        /// convergence trace.
        /// </remarks>
        /// <param name="request">Depot, stops, fleet parameters, and search budget.</param>
        /// <returns>Routes, unserved stops, objective trace, and honest timings.</returns>
        [HttpPost("optimize")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(OptimizeResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<OptimizeResultDto>> Optimize(
            [FromBody] OptimizeRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _fleetRoutingService.OptimizeAsync(request, cancellationToken));
        }
    }
}
