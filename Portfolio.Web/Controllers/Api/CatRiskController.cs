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
    /// API endpoints for catastrophe risk analytics: exposure concentration (ring
    /// accumulation) and Monte Carlo event-loss simulation producing AAL, PML, and the
    /// occurrence exceedance probability curve.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [EnableRateLimiting("expensive")]
    [CpuBound]
    [Route("api/v{version:apiVersion}/catrisk")]
    public class CatRiskController : ControllerBase
    {
        private readonly ICatRiskService _catRiskService;

        public CatRiskController(ICatRiskService catRiskService)
        {
            _catRiskService = catRiskService;
        }

        /// <summary>Returns the demo policy book and its paired stochastic wildfire event catalog.</summary>
        /// <returns>Insured locations, event catalog, and per-community exposure rollups.</returns>
        [HttpGet("book")]
        [ProducesResponseType(typeof(PolicyBookDto), 200)]
        [ResponseCache(Duration = 3600)]
        public async Task<ActionResult<PolicyBookDto>> GetBook(CancellationToken cancellationToken)
        {
            var result = await _catRiskService.GetPolicyBookAsync(cancellationToken);
            return Ok(result);
        }

        /// <summary>Computes ring accumulation across a book and flags concentration breaches.</summary>
        /// <remarks>A location is flagged when its concentrated TIV exceeds the supplied limit.</remarks>
        /// <param name="request">Policy locations, ring radius, and concentration limit.</param>
        /// <returns>Per-location ring totals plus a breach tally and the worst offender.</returns>
        [HttpPost("accumulation")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(AccumulationResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(8_000_000)]
        public async Task<ActionResult<AccumulationResultDto>> Accumulation(
            [FromBody] AccumulationRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _catRiskService.ComputeAccumulationAsync(request, cancellationToken));
        }

        /// <summary>Simulates a stochastic event catalog against a book.</summary>
        /// <remarks>
        /// Returns the average annual loss, the probable maximum loss, and the occurrence exceedance probability curve.
        /// </remarks>
        /// <param name="request">Policy locations, event catalog, and vulnerability curve shape.</param>
        /// <returns>AAL, PML, benchmark return-period losses, and the OEP curve.</returns>
        [HttpPost("simulate")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(SimulationResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(8_000_000)]
        public async Task<ActionResult<SimulationResultDto>> Simulate(
            [FromBody] SimulationRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _catRiskService.SimulateAsync(request, cancellationToken));
        }
    }
}
