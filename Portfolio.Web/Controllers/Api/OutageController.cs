using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Web.RateLimiting;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Filters;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for distribution outage management: fault tracing over a radial
    /// feeder, isolation-device selection, and tie-switch restoration planning.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [EnableRateLimiting("expensive")]
    [CpuBound]
    [Route("api/v{version:apiVersion}/outage")]
    public class OutageController : ControllerBase
    {
        private readonly IOutageTraceService _outageTraceService;

        public OutageController(IOutageTraceService outageTraceService)
        {
            _outageTraceService = outageTraceService;
        }

        /// <summary>Returns the demo distribution network.</summary>
        /// <remarks>
        /// The network has one substation, two radial feeders, and the normally-open tie switch between them.
        /// </remarks>
        /// <returns>Every network element with its device type, state, and geometry.</returns>
        [HttpGet("network")]
        [ProducesResponseType(typeof(DistributionNetworkDto), 200)]
        [ResponseCache(Duration = 3600)]
        public async Task<ActionResult<DistributionNetworkDto>> GetNetwork(CancellationToken cancellationToken)
        {
            var result = await _outageTraceService.GetNetworkAsync(cancellationToken);
            return Ok(result);
        }

        /// <summary>Traces a fault on the supplied network.</summary>
        /// <remarks>
        /// The trace covers the de-energized downstream section, the upstream path back to the source, and the devices
        /// that isolate it.
        /// </remarks>
        /// <param name="request">Network elements, source node, and faulted element.</param>
        /// <returns>Downstream, upstream, and isolation element ids plus the customer impact.</returns>
        [HttpPost("trace")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(TraceResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<TraceResultDto>> Trace(
            [FromBody] TraceRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _outageTraceService.TraceAsync(request, cancellationToken));
        }

        /// <summary>Proposes the switching plan that restores the most customers around an isolated fault.</summary>
        /// <remarks>The plan closes one normally-open tie.</remarks>
        /// <param name="request">Network elements, faulted element, and the isolation devices to open.</param>
        /// <returns>An ordered switching plan with the customers it restores.</returns>
        [HttpPost("restore")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(RestoreResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<RestoreResultDto>> Restore(
            [FromBody] RestoreRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _outageTraceService.ProposeRestorationAsync(request, cancellationToken));
        }
    }
}
