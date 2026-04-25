using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for batch geocoding CSV uploads.
    /// </summary>
    [ApiController]
    [Route("api/batchgeocoding")]
    [AllowAnonymous]
    public class BatchGeocodingController : ControllerBase
    {
        private readonly IBatchGeocodingService _batchGeocodingService;
        private readonly ILogger<BatchGeocodingController> _logger;

        public BatchGeocodingController(IBatchGeocodingService batchGeocodingService, ILogger<BatchGeocodingController> logger)
        {
            _batchGeocodingService = batchGeocodingService;
            _logger = logger;
        }

        /// <summary>
        /// Accepts a CSV file upload of addresses and returns geocoding results.
        /// The CSV must have columns: Id, Address, City, State, Zip.
        /// </summary>
        /// <param name="file">CSV file containing addresses to geocode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of geocoding results with match status, coordinates, and score.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(List<BatchGeocodingResultDto>), 200)]
        [ProducesResponseType(typeof(object), 400)]
        [ProducesResponseType(typeof(object), 500)]
        public async Task<IActionResult> GeocodeAddresses(IFormFile file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { error = "A non-empty CSV file is required." });

            try
            {
                var results = await _batchGeocodingService.GeocodeAsync(file, cancellationToken);
                return Ok(results);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
