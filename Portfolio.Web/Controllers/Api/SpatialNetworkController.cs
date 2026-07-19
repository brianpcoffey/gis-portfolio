using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for spatial graph routing — road graph retrieval, shortest-path
    /// routing (Dijkstra/A*), and service-area computation.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [Route("api/v{version:apiVersion}/network")]
    public class SpatialNetworkController : ControllerBase
    {
        private readonly ISpatialGraphService _spatialGraphService;
        private readonly ILogger<SpatialNetworkController> _logger;

        public SpatialNetworkController(
            ISpatialGraphService spatialGraphService,
            ILogger<SpatialNetworkController> logger)
        {
            _spatialGraphService = spatialGraphService;
            _logger = logger;
        }

        /// <summary>
        /// Returns the pre-built Redlands, CA road graph centered on Esri HQ.
        /// </summary>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Graph nodes, edges, and destination node id.</returns>
        [HttpGet("graph")]
        [ProducesResponseType(typeof(RoadGraphDto), 200)]
        public async Task<ActionResult<RoadGraphDto>> GetGraph(CancellationToken cancellationToken)
        {
            var graph = await _spatialGraphService.GetRedlandsGraphAsync(cancellationToken);
            return Ok(graph);
        }

        /// <summary>
        /// Computes the least-cost route between two graph nodes.
        /// </summary>
        /// <param name="request">Spatial graph and route endpoints.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Route path and total cost.</returns>
        [HttpPost("route")]
        [ProducesResponseType(typeof(RouteResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<RouteResultDto>> Route(
            [FromBody] RouteRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _spatialGraphService.FindShortestPathAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid spatial network route request.");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Computes graph nodes reachable from an origin within a cost budget.
        /// </summary>
        /// <param name="request">Spatial graph, origin, and maximum cost.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Reachable node ids.</returns>
        [HttpPost("service-area")]
        [ProducesResponseType(typeof(ServiceAreaResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<ServiceAreaResultDto>> ServiceArea(
            [FromBody] ServiceAreaRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _spatialGraphService.ComputeServiceAreaAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid spatial network service-area request.");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
