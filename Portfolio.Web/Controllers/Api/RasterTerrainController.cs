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
    [Route("api/v{version:apiVersion}/raster")]
    public class RasterTerrainController : ControllerBase
    {
        private readonly IRasterTerrainService _rasterTerrainService;
        private readonly ILogger<RasterTerrainController> _logger;

        public RasterTerrainController(
            IRasterTerrainService rasterTerrainService,
            ILogger<RasterTerrainController> logger)
        {
            _rasterTerrainService = rasterTerrainService;
            _logger = logger;
        }

        /// <summary>
        /// Generates an 8-bit hillshade intensity grid from an elevation raster.
        /// </summary>
        /// <param name="request">Elevation raster and hillshade options.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Hillshade intensity grid.</returns>
        [HttpPost("hillshade")]
        [ProducesResponseType(typeof(RasterHillshadeResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(8_000_000)]
        public async Task<ActionResult<RasterHillshadeResultDto>> Hillshade(
            [FromBody] RasterHillshadeRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _rasterTerrainService.GenerateHillshadeAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid raster hillshade request.");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Generates a normalized heatmap over a raster extent.
        /// </summary>
        /// <param name="request">Weighted points and raster extent.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Normalized heatmap values.</returns>
        [HttpPost("heatmap")]
        [ProducesResponseType(typeof(HeatmapResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(4_000_000)]
        public async Task<ActionResult<HeatmapResultDto>> Heatmap(
            [FromBody] HeatmapRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _rasterTerrainService.GenerateHeatmapAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid raster heatmap request.");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
