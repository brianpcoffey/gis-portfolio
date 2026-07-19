using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for emergency response coverage: drive-time isochrones over the road
    /// network and p-median station siting evaluated against NFPA 1710.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [EnableRateLimiting("expensive")]
    [Route("api/v{version:apiVersion}/response")]
    public class ResponseCoverageController : ControllerBase
    {
        private readonly IResponseCoverageService _responseCoverageService;
        private readonly ILogger<ResponseCoverageController> _logger;

        public ResponseCoverageController(
            IResponseCoverageService responseCoverageService,
            ILogger<ResponseCoverageController> logger)
        {
            _responseCoverageService = responseCoverageService;
            _logger = logger;
        }

        /// <summary>
        /// Returns the demo response scenario: clustered call demand, candidate station
        /// sites, and the stations operating today.
        /// </summary>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Demand points, candidate sites, and the existing station ids.</returns>
        [HttpGet("scenario")]
        [ProducesResponseType(typeof(ResponseScenarioDto), 200)]
        [ResponseCache(Duration = 3600)]
        public async Task<ActionResult<ResponseScenarioDto>> GetScenario(CancellationToken cancellationToken)
        {
            var result = await _responseCoverageService.GetScenarioAsync(cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Computes drive-time bands over the road network from one origin node.
        /// </summary>
        /// <param name="request">Origin node, apparatus speed, and ascending band bounds in minutes.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Every reachable node with its travel time and band index.</returns>
        [HttpPost("isochrone")]
        [ProducesResponseType(typeof(IsochroneResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<IsochroneResultDto>> Isochrone(
            [FromBody] IsochroneRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _responseCoverageService.ComputeIsochroneAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid isochrone request.");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Sites the requested number of stations against weighted demand and reports the
        /// result beside the stations operating today.
        /// </summary>
        /// <param name="request">Demand points, candidate sites, station count, and objective.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Chosen stations, optimized and baseline coverage statistics, and assignments.</returns>
        [HttpPost("optimize")]
        [ProducesResponseType(typeof(OptimizeCoverageResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<OptimizeCoverageResultDto>> Optimize(
            [FromBody] OptimizeCoverageRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _responseCoverageService.OptimizeAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid coverage optimization request.");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
