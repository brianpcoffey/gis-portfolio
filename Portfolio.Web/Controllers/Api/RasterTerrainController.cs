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
    /// <summary>
    /// API endpoints for raster terrain analysis (hillshade and heatmap generation
    /// over dense numeric grids).
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [EnableRateLimiting("compute")]
    [CpuBound]
    [Route("api/v{version:apiVersion}/raster")]
    public class RasterTerrainController : ControllerBase
    {
        private readonly IRasterTerrainService _rasterTerrainService;

        public RasterTerrainController(IRasterTerrainService rasterTerrainService)
        {
            _rasterTerrainService = rasterTerrainService;
        }

        /// <summary>Generates an 8-bit hillshade intensity grid from an elevation raster.</summary>
        /// <param name="request">Elevation raster and hillshade options.</param>
        /// <returns>Hillshade intensity grid.</returns>
        [HttpPost("hillshade")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(RasterHillshadeResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(8_000_000)]
        public async Task<ActionResult<RasterHillshadeResultDto>> Hillshade(
            [FromBody] RasterHillshadeRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _rasterTerrainService.GenerateHillshadeAsync(request, cancellationToken));
        }

        /// <summary>Generates a normalized heatmap over a raster extent.</summary>
        /// <param name="request">Weighted points and raster extent.</param>
        /// <returns>Normalized heatmap values.</returns>
        [HttpPost("heatmap")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(HeatmapResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<HeatmapResultDto>> Heatmap(
            [FromBody] HeatmapRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _rasterTerrainService.GenerateHeatmapAsync(request, cancellationToken));
        }
    }
}
