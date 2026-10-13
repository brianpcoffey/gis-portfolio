using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Common.Constants;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Filters;
using Portfolio.Web.RateLimiting;

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
    [CpuBound]
    [Route("api/v{version:apiVersion}/change")]
    public class ChangeDetectionController : ControllerBase
    {
        private readonly IChangeDetectionService _changeDetectionService;

        public ChangeDetectionController(IChangeDetectionService changeDetectionService)
        {
            _changeDetectionService = changeDetectionService;
        }

        /// <summary>Builds the deterministic synthetic two-epoch, four-band scene.</summary>
        /// <remarks>Four planted changes are exposed as ground truth.</remarks>
        /// <param name="width">Raster width in pixels, up to 512.</param>
        /// <param name="height">Raster height in pixels, up to 512.</param>
        /// <param name="noise">Gaussian noise standard deviation applied to both epochs.</param>
        /// <returns>Both epochs, band names, and the ground-truth change boxes.</returns>
        [HttpGet("scene")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(ChangeSceneDto), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ChangeSceneDto>> Scene(
            [FromQuery] int width = 256,
            [FromQuery] int height = 256,
            [FromQuery] double noise = ChangeSceneDefaults.NoiseLevel,
            CancellationToken cancellationToken = default)
        {
            return Ok(await _changeDetectionService.GetSceneAsync(width, height, noise, cancellationToken));
        }

        /// <summary>Runs the change detection pipeline over a co-registered two-epoch stack.</summary>
        /// <remarks>
        /// Returns the magnitude raster, the change mask, the Otsu histogram, and the ranked detections.
        /// </remarks>
        /// <param name="request">Both epochs plus threshold, morphology, and filtering settings.</param>
        /// <returns>CVA magnitude, change mask, histogram, and ranked change blobs.</returns>
        [HttpPost("detect")]
        [BadRequestOnArgumentException]
        [ProducesResponseType(typeof(DetectResultDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)]
        [RequestSizeLimit(16_000_000)]
        public async Task<ActionResult<DetectResultDto>> Detect(
            [FromBody] DetectRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await _changeDetectionService.DetectAsync(request, cancellationToken));
        }
    }
}
