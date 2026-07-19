using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for line-of-sight viewshed analysis over dense elevation grids.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [Route("api/v{version:apiVersion}/viewshed")]
    public class ViewshedController : ControllerBase
    {
        private readonly IViewshedService _viewshedService;
        private readonly ILogger<ViewshedController> _logger;

        public ViewshedController(
            IViewshedService viewshedService,
            ILogger<ViewshedController> logger)
        {
            _viewshedService = viewshedService;
            _logger = logger;
        }

        /// <summary>
        /// Computes the set of cells visible from an observer position over an elevation grid.
        /// </summary>
        /// <param name="request">Elevation grid, observer position, and observer height.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Per-cell visibility flags plus a visible-cell count.</returns>
        [HttpPost("compute")]
        [ProducesResponseType(typeof(ViewshedResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(8_000_000)]
        public async Task<ActionResult<ViewshedResultDto>> Compute(
            [FromBody] ViewshedRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _viewshedService.ComputeAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid viewshed request.");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
