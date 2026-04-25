using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for reverse geocoding a coordinate to structured place data.
    /// </summary>
    [ApiController]
    [Route("api/reversegeocoding")]
    [AllowAnonymous]
    public class ReverseGeocodingController : ControllerBase
    {
        private readonly IReverseGeocodingService _reverseGeocodingService;
        private readonly ILogger<ReverseGeocodingController> _logger;

        public ReverseGeocodingController(IReverseGeocodingService reverseGeocodingService, ILogger<ReverseGeocodingController> logger)
        {
            _reverseGeocodingService = reverseGeocodingService;
            _logger = logger;
        }

        /// <summary>
        /// Accepts a WGS84 latitude and longitude and returns structured place data for that location.
        /// </summary>
        /// <param name="lat">WGS84 latitude (-90 to 90).</param>
        /// <param name="lng">WGS84 longitude (-180 to 180).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Structured place data including address components and location type.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(ReverseGeocodingResultDto), 200)]
        [ProducesResponseType(typeof(object), 400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetPlaceData([FromQuery] double lat, [FromQuery] double lng, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _reverseGeocodingService.ReverseGeocodeAsync(lat, lng, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }
    }
}
