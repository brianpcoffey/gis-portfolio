using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for address parsing, standardization, and validation.
    /// </summary>
    [ApiController]
    [Route("api/addressstandardization")]
    [AllowAnonymous]
    public class AddressStandardizationController : ControllerBase
    {
        private readonly IAddressStandardizationService _addressStandardizationService;
        private readonly ILogger<AddressStandardizationController> _logger;

        public AddressStandardizationController(
            IAddressStandardizationService addressStandardizationService,
            ILogger<AddressStandardizationController> logger)
        {
            _addressStandardizationService = addressStandardizationService;
            _logger = logger;
        }

        /// <summary>
        /// Parses a raw freeform address string and returns structured, standardized address components.
        /// </summary>
        /// <param name="request">Request body containing the raw address string.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Structured address components including a standardized single-line output and parse confidence score.</returns>
        [HttpPost("parse")]
        [ProducesResponseType(typeof(AddressParsedDto), 200)]
        [ProducesResponseType(typeof(object), 400)]
        public async Task<IActionResult> Parse([FromBody] AddressParseRequestDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request?.RawAddress))
                return BadRequest(new { error = "RawAddress is required." });

            try
            {
                var result = await _addressStandardizationService.ParseAsync(request.RawAddress, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Parses a raw freeform address, geocodes it against ArcGIS, and returns validation results
        /// including the matched address, confidence score, and confidence tier classification.
        /// </summary>
        /// <param name="request">Request body containing the raw address string.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Validation result with parsed components, ArcGIS matched address, score, and confidence tier.</returns>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(AddressValidationResultDto), 200)]
        [ProducesResponseType(typeof(object), 400)]
        public async Task<IActionResult> Validate([FromBody] AddressParseRequestDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request?.RawAddress))
                return BadRequest(new { error = "RawAddress is required." });

            try
            {
                var result = await _addressStandardizationService.ValidateAsync(request.RawAddress, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
