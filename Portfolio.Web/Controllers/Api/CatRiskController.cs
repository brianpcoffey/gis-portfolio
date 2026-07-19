using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

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
    [Route("api/v{version:apiVersion}/catrisk")]
    public class CatRiskController : ControllerBase
    {
        private readonly ICatRiskService _catRiskService;
        private readonly ILogger<CatRiskController> _logger;

        public CatRiskController(
            ICatRiskService catRiskService,
            ILogger<CatRiskController> logger)
        {
            _catRiskService = catRiskService;
            _logger = logger;
        }

        /// <summary>
        /// Returns the demo policy book and its paired stochastic wildfire event catalog.
        /// </summary>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Insured locations, event catalog, and per-community exposure rollups.</returns>
        [HttpGet("book")]
        [ProducesResponseType(typeof(PolicyBookDto), 200)]
        [ResponseCache(Duration = 3600)]
        public async Task<ActionResult<PolicyBookDto>> GetBook(CancellationToken cancellationToken)
        {
            var result = await _catRiskService.GetPolicyBookAsync(cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Computes ring accumulation across a book and flags locations whose concentrated
        /// TIV exceeds the supplied limit.
        /// </summary>
        /// <param name="request">Policy locations, ring radius, and concentration limit.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Per-location ring totals plus a breach tally and the worst offender.</returns>
        [HttpPost("accumulation")]
        [ProducesResponseType(typeof(AccumulationResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(8_000_000)]
        public async Task<ActionResult<AccumulationResultDto>> Accumulation(
            [FromBody] AccumulationRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _catRiskService.ComputeAccumulationAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid catastrophe accumulation request.");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Simulates a stochastic event catalog against a book, returning the average annual
        /// loss, the probable maximum loss, and the occurrence exceedance probability curve.
        /// </summary>
        /// <param name="request">Policy locations, event catalog, and vulnerability curve shape.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>AAL, PML, benchmark return-period losses, and the OEP curve.</returns>
        [HttpPost("simulate")]
        [ProducesResponseType(typeof(SimulationResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(8_000_000)]
        public async Task<ActionResult<SimulationResultDto>> Simulate(
            [FromBody] SimulationRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _catRiskService.SimulateAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid catastrophe simulation request.");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
