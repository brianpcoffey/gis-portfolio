using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for density-based spatial clustering (DBSCAN hotspot detection).
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [Route("api/v{version:apiVersion}/clustering")]
    public class SpatialClusterController : ControllerBase
    {
        private readonly ISpatialClusterService _clusterService;
        private readonly ILogger<SpatialClusterController> _logger;

        public SpatialClusterController(
            ISpatialClusterService clusterService,
            ILogger<SpatialClusterController> logger)
        {
            _clusterService = clusterService;
            _logger = logger;
        }

        /// <summary>
        /// Clusters a set of points with DBSCAN, returning per-point cluster labels and hotspot summaries.
        /// </summary>
        /// <param name="request">Points plus epsilon and minimum-points parameters.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Cluster labels, cluster sizes, and noise count.</returns>
        [HttpPost("dbscan")]
        [ProducesResponseType(typeof(DbscanResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<DbscanResultDto>> RunDbscan(
            [FromBody] DbscanRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _clusterService.RunDbscanAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid DBSCAN clustering request.");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
