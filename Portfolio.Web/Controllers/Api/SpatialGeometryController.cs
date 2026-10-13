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
    /// API endpoints for computational geometry operations (fan triangulation and
    /// bounding-box polygon clipping).
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [EnableRateLimiting("compute")]
    [CpuBound]
    [Route("api/v{version:apiVersion}/geometry")]
    public class SpatialGeometryController : ControllerBase
    {
        private readonly ISpatialGeometryService _spatialGeometryService;

        public SpatialGeometryController(ISpatialGeometryService spatialGeometryService)
        {
            _spatialGeometryService = spatialGeometryService;
        }

        /// <summary>Triangulates a point set for map visualization.</summary>
        /// <param name="request">Point set to triangulate.</param>
        /// <returns>Triangle mesh result.</returns>
        [HttpPost("triangulate")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(TriangulationResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(1_000_000)]
        public async Task<ActionResult<TriangulationResultDto>> Triangulate(
            [FromBody] GeometryPointSetDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _spatialGeometryService.TriangulateAsync(request, cancellationToken));
        }

        /// <summary>Clips polygon vertices to a bounding box.</summary>
        /// <param name="request">Polygon and bounding box request.</param>
        /// <returns>Clipped polygon operation result.</returns>
        [HttpPost("clip")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(PolygonOperationResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(1_000_000)]
        public async Task<ActionResult<PolygonOperationResultDto>> Clip(
            [FromBody] PolygonClipRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _spatialGeometryService.ClipToBoundingBoxAsync(request, cancellationToken));
        }
    }
}
