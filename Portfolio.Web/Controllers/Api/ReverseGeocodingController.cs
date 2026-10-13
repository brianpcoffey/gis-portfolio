using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Common.DTOs;
using Portfolio.Common.Enums;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Filters;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for reverse geocoding a coordinate to structured place data.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/geocoding/reverse")]
    [AllowAnonymous]
    [EnableRateLimiting("expensive")] // calls the paid ArcGIS geocoding API
    public class ReverseGeocodingController : ControllerBase
    {
        private readonly IReverseGeocodingService _reverseGeocodingService;

        public ReverseGeocodingController(IReverseGeocodingService reverseGeocodingService)
        {
            _reverseGeocodingService = reverseGeocodingService;
        }

        /// <summary>
        /// Accepts a WGS84 latitude and longitude and returns structured place data for that location.
        /// </summary>
        /// <param name="lat">WGS84 latitude (-90 to 90).</param>
        /// <param name="lng">WGS84 longitude (-180 to 180).</param>
        /// <returns>Structured place data including address components and location type.</returns>
        [HttpGet]
        [BadRequestOnArgumentException]
        [ArcGisUnavailable(ArcGisUpstream.Geocoding)]
        [ProducesResponseType(typeof(ReverseGeocodingResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPlaceData([FromQuery] double lat, [FromQuery] double lng, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _reverseGeocodingService.ReverseGeocodeAsync(lat, lng, cancellationToken);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }
    }
}

