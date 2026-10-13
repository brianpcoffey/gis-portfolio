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
    /// <summary>API endpoints for point-in-polygon spatial joins (assigning points to zones).</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [EnableRateLimiting("compute")]
    [CpuBound]
    [Route("api/v{version:apiVersion}/overlay")]
    public class SpatialOverlayController : ControllerBase
    {
        private readonly ISpatialOverlayService _overlayService;

        public SpatialOverlayController(ISpatialOverlayService overlayService)
        {
            _overlayService = overlayService;
        }

        /// <summary>Tags each point with the first zone that contains it and rolls up per-zone counts.</summary>
        /// <param name="request">Points and candidate zone polygons.</param>
        /// <returns>Tagged points plus per-zone summaries and an unassigned tally.</returns>
        [HttpPost("spatial-join")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(SpatialJoinResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<SpatialJoinResultDto>> SpatialJoin(
            [FromBody] SpatialJoinRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _overlayService.SpatialJoinAsync(request, cancellationToken));
        }
    }
}
