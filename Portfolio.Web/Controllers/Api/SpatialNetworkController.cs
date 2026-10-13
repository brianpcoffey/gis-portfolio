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
    /// API endpoints for spatial graph routing — road graph retrieval, shortest-path
    /// routing (Dijkstra/A*), and service-area computation.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [EnableRateLimiting("compute")]
    [CpuBound]
    [Route("api/v{version:apiVersion}/network")]
    public class SpatialNetworkController : ControllerBase
    {
        private readonly ISpatialGraphService _spatialGraphService;

        public SpatialNetworkController(ISpatialGraphService spatialGraphService)
        {
            _spatialGraphService = spatialGraphService;
        }

        /// <summary>Returns the pre-built Redlands, CA road graph centered on Esri HQ.</summary>
        /// <returns>Graph nodes, edges, and destination node id.</returns>
        [HttpGet("graph")]
        [ProducesResponseType(typeof(RoadGraphDto), 200)]
        public async Task<ActionResult<RoadGraphDto>> GetGraph(CancellationToken cancellationToken)
        {
            var graph = await _spatialGraphService.GetRedlandsGraphAsync(cancellationToken);
            return Ok(graph);
        }

        /// <summary>Computes the least-cost route between two graph nodes.</summary>
        /// <param name="request">Spatial graph and route endpoints.</param>
        /// <returns>Route path and total cost.</returns>
        [HttpPost("route")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(RouteResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<RouteResultDto>> Route(
            [FromBody] RouteRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _spatialGraphService.FindShortestPathAsync(request, cancellationToken));
        }

        /// <summary>Computes graph nodes reachable from an origin within a cost budget.</summary>
        /// <param name="request">Spatial graph, origin, and maximum cost.</param>
        /// <returns>Reachable node ids.</returns>
        [HttpPost("service-area")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(ServiceAreaResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<ServiceAreaResultDto>> ServiceArea(
            [FromBody] ServiceAreaRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _spatialGraphService.ComputeServiceAreaAsync(request, cancellationToken));
        }
    }
}
