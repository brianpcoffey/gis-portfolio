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
    /// API endpoints for emergency response coverage: drive-time isochrones over the road
    /// network and p-median station siting evaluated against NFPA 1710.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [EnableRateLimiting("expensive")]
    [CpuBound]
    [Route("api/v{version:apiVersion}/response")]
    public class ResponseCoverageController : ControllerBase
    {
        private readonly IResponseCoverageService _responseCoverageService;

        public ResponseCoverageController(IResponseCoverageService responseCoverageService)
        {
            _responseCoverageService = responseCoverageService;
        }

        /// <summary>Returns the demo response scenario.</summary>
        /// <remarks>
        /// The scenario has clustered call demand, candidate station sites, and the stations operating today.
        /// </remarks>
        /// <returns>Demand points, candidate sites, and the existing station ids.</returns>
        [HttpGet("scenario")]
        [ProducesResponseType(typeof(ResponseScenarioDto), 200)]
        [ResponseCache(Duration = 3600)]
        public async Task<ActionResult<ResponseScenarioDto>> GetScenario(CancellationToken cancellationToken)
        {
            var result = await _responseCoverageService.GetScenarioAsync(cancellationToken);
            return Ok(result);
        }

        /// <summary>Computes drive-time bands over the road network from one origin node.</summary>
        /// <param name="request">Origin node, apparatus speed, and ascending band bounds in minutes.</param>
        /// <returns>Every reachable node with its travel time and band index.</returns>
        [HttpPost("isochrone")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(IsochroneResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<IsochroneResultDto>> Isochrone(
            [FromBody] IsochroneRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _responseCoverageService.ComputeIsochroneAsync(request, cancellationToken));
        }

        /// <summary>Sites the requested number of stations against weighted demand.</summary>
        /// <remarks>Reports the result beside the stations operating today.</remarks>
        /// <param name="request">Demand points, candidate sites, station count, and objective.</param>
        /// <returns>Chosen stations, optimized and baseline coverage statistics, and assignments.</returns>
        [HttpPost("optimize")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(OptimizeCoverageResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<OptimizeCoverageResultDto>> Optimize(
            [FromBody] OptimizeCoverageRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _responseCoverageService.OptimizeAsync(request, cancellationToken));
        }
    }
}
