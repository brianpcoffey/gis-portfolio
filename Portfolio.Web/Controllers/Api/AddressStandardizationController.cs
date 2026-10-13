using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Filters;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for address parsing, standardization, and validation.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/addresses")]
    [AllowAnonymous]
    [EnableRateLimiting("expensive")] // calls the paid ArcGIS geocoding API
    public class AddressStandardizationController : ControllerBase
    {
        private readonly IAddressStandardizationService _addressStandardizationService;

        public AddressStandardizationController(IAddressStandardizationService addressStandardizationService)
        {
            _addressStandardizationService = addressStandardizationService;
        }

        /// <summary>
        /// Parses a raw freeform address string and returns structured, standardized address components.
        /// </summary>
        /// <param name="request">Request body containing the raw address string.</param>
        /// <returns>Structured address components including a standardized single-line output and parse confidence score.</returns>
        [HttpPost("parse")]
        [BadRequestOnArgumentException]
        [ArcGisUnavailable(ArcGisUpstream.Geocoding)]
        [RequestSizeLimit(16_384)]
        [ProducesResponseType(typeof(AddressParsedDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Parse([FromBody] AddressParseRequestDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request?.RawAddress))
                return BadRequest(new { error = "RawAddress is required." });

            return Ok(await _addressStandardizationService.ParseAsync(request.RawAddress, cancellationToken));
        }

        /// <summary>
        /// Parses a raw freeform address, geocodes it against ArcGIS, and returns validation results
        /// including the matched address, confidence score, and confidence tier classification.
        /// </summary>
        /// <param name="request">Request body containing the raw address string.</param>
        /// <returns>Validation result with parsed components, ArcGIS matched address, score, and confidence tier.</returns>
        [HttpPost("validate")]
        [BadRequestOnArgumentException]
        [ArcGisUnavailable(ArcGisUpstream.Geocoding)]
        [RequestSizeLimit(16_384)]
        [ProducesResponseType(typeof(AddressValidationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Validate([FromBody] AddressParseRequestDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request?.RawAddress))
                return BadRequest(new { error = "RawAddress is required." });

            return Ok(await _addressStandardizationService.ValidateAsync(request.RawAddress, cancellationToken));
        }
    }
}

