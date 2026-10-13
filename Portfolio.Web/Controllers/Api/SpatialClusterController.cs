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
    /// <summary>API endpoints for density-based spatial clustering (DBSCAN hotspot detection).</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [EnableRateLimiting("compute")]
    [CpuBound]
    [Route("api/v{version:apiVersion}/clustering")]
    public class SpatialClusterController : ControllerBase
    {
        private readonly ISpatialClusterService _clusterService;

        public SpatialClusterController(ISpatialClusterService clusterService)
        {
            _clusterService = clusterService;
        }

        /// <summary>
        /// Clusters a set of points with DBSCAN, returning per-point cluster labels and hotspot summaries.
        /// </summary>
        /// <param name="request">Points plus epsilon and minimum-points parameters.</param>
        /// <returns>Cluster labels, cluster sizes, and noise count.</returns>
        [HttpPost("dbscan")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(DbscanResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<DbscanResultDto>> RunDbscan(
            [FromBody] DbscanRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _clusterService.RunDbscanAsync(request, cancellationToken));
        }
    }
}
