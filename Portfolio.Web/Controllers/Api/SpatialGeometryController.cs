using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [Route("api/v{version:apiVersion}/geometry")]
    public class SpatialGeometryController : ControllerBase
    {
        private readonly ISpatialGeometryService _spatialGeometryService;
        private readonly ILogger<SpatialGeometryController> _logger;

        public SpatialGeometryController(
            ISpatialGeometryService spatialGeometryService,
            ILogger<SpatialGeometryController> logger)
        {
            _spatialGeometryService = spatialGeometryService;
            _logger = logger;
        }

        /// <summary>
        /// Triangulates a point set for map visualization.
        /// </summary>
        /// <param name="request">Point set to triangulate.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Triangle mesh result.</returns>
        [HttpPost("triangulate")]
        [ProducesResponseType(typeof(TriangulationResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(1_000_000)]
        public async Task<ActionResult<TriangulationResultDto>> Triangulate(
            [FromBody] GeometryPointSetDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _spatialGeometryService.TriangulateAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid spatial geometry triangulation request.");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Clips polygon vertices to a bounding box.
        /// </summary>
        /// <param name="request">Polygon and bounding box request.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Clipped polygon operation result.</returns>
        [HttpPost("clip")]
        [ProducesResponseType(typeof(PolygonOperationResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(1_000_000)]
        public async Task<ActionResult<PolygonOperationResultDto>> Clip(
            [FromBody] PolygonClipRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _spatialGeometryService.ClipToBoundingBoxAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid spatial geometry clipping request.");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
