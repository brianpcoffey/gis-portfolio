using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for multitemporal raster change detection: the synthetic two-epoch
    /// scene and the Change Vector Analysis / Otsu / morphology / connected-component
    /// detection pipeline.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [EnableRateLimiting("expensive")]
    [Route("api/v{version:apiVersion}/change")]
    public class ChangeDetectionController : ControllerBase
    {
        private readonly IChangeDetectionService _changeDetectionService;
        private readonly ILogger<ChangeDetectionController> _logger;

        public ChangeDetectionController(
            IChangeDetectionService changeDetectionService,
            ILogger<ChangeDetectionController> logger)
        {
            _changeDetectionService = changeDetectionService;
            _logger = logger;
        }

        /// <summary>
        /// Builds the deterministic synthetic two-epoch, four-band scene with four planted
        /// changes exposed as ground truth.
        /// </summary>
        /// <param name="width">Raster width in pixels, up to 512.</param>
        /// <param name="height">Raster height in pixels, up to 512.</param>
        /// <param name="noise">Gaussian noise standard deviation applied to both epochs.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>Both epochs, band names, and the ground-truth change boxes.</returns>
        [HttpGet("scene")]
        [ProducesResponseType(typeof(ChangeSceneDto), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ChangeSceneDto>> Scene(
            [FromQuery] int width = 256,
            [FromQuery] int height = 256,
            [FromQuery] double noise = 0.025,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _changeDetectionService.GetSceneAsync(width, height, noise, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid change detection scene request.");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Runs the change detection pipeline over a co-registered two-epoch stack and
        /// returns the magnitude raster, the change mask, the Otsu histogram, and the
        /// ranked detections.
        /// </summary>
        /// <param name="request">Both epochs plus threshold, morphology, and filtering settings.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>CVA magnitude, change mask, histogram, and ranked change blobs.</returns>
        [HttpPost("detect")]
        [ProducesResponseType(typeof(DetectResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(16_000_000)]
        public async Task<ActionResult<DetectResultDto>> Detect(
            [FromBody] DetectRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _changeDetectionService.DetectAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid change detection request.");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
