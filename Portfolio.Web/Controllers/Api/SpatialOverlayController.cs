using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for point-in-polygon spatial joins (assigning points to zones).
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [Route("api/v{version:apiVersion}/overlay")]
    public class SpatialOverlayController : ControllerBase
    {
        private readonly ISpatialOverlayService _overlayService;
        private readonly ILogger<SpatialOverlayController> _logger;

        public SpatialOverlayController(
            ISpatialOverlayService overlayService,
            ILogger<SpatialOverlayController> logger)
        {
            _overlayService = overlayService;
            _logger = logger;
        }

        /// <summary>
        /// Tags each point with the first zone that contains it and rolls up per-zone counts.
        /// </summary>
        /// <param name="request">Points and candidate zone polygons.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Tagged points plus per-zone summaries and an unassigned tally.</returns>
        [HttpPost("spatial-join")]
        [ProducesResponseType(typeof(SpatialJoinResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<SpatialJoinResultDto>> SpatialJoin(
            [FromBody] SpatialJoinRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _overlayService.SpatialJoinAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid spatial join request.");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
