using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Services.Abstractions;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for batch geocoding CSV uploads.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/geocoding/batch")]
    [AllowAnonymous]
    [EnableRateLimiting("expensive")] // fans out to the paid ArcGIS geocoding API
    public class BatchGeocodingController : ControllerBase
    {
        private readonly IBatchGeocodingService _batchGeocodingService;
        private readonly IBatchJobStore _jobStore;
        private readonly ILogger<BatchGeocodingController> _logger;

        public BatchGeocodingController(
            IBatchGeocodingService batchGeocodingService,
            IBatchJobStore jobStore,
            ILogger<BatchGeocodingController> logger)
        {
            _batchGeocodingService = batchGeocodingService;
            _jobStore = jobStore;
            _logger = logger;
        }

        // ~2 MB cap on the uploaded CSV (paired with the row-count cap in the service).
        private const int MaxUploadBytes = 2 * 1024 * 1024;

        // Accept only .csv uploads — defence-in-depth alongside the size limit and row cap.
        // string.Equals is null-safe, so a multipart part with no filename yields a clean false.
        private static bool HasCsvExtension(IFormFile file) =>
            string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Accepts a CSV file upload of addresses, enqueues a background geocoding job,
        /// and returns 202 Accepted with a status URL the client can poll.
        /// The CSV must have columns: Id, Address, City, State, Zip.
        /// </summary>
        /// <param name="file">CSV file containing addresses to geocode.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Job ID and status polling URL.</returns>
        [HttpPost]
        [RequestSizeLimit(MaxUploadBytes)]
        [ProducesResponseType(typeof(BatchJobAcceptedDto), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SubmitAsync(IFormFile file, CancellationToken ct)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new ProblemDetails { Title = "File required", Status = 400 });
            if (!HasCsvExtension(file))
                return BadRequest(new ProblemDetails { Title = "A .csv file is required", Status = 400 });

            try
            {
                var jobId   = await _batchGeocodingService.EnqueueAsync(file, ct);
                var baseUrl = $"{Request.Scheme}://{Request.Host}";

                return Accepted(new BatchJobAcceptedDto(
                    JobId:     jobId,
                    StatusUrl: $"{baseUrl}/api/v1/geocoding/batch/{jobId}/status"
                ));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Polly.CircuitBreaker.BrokenCircuitException)
            {
                _logger.LogWarning("ArcGIS circuit breaker is open. Geocoding temporarily unavailable.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
                {
                    Status = 503,
                    Title  = "Geocoding Unavailable",
                    Detail = "The upstream geocoding service is temporarily unavailable. Retry after 15 seconds.",
                    Extensions = { ["retryAfterSeconds"] = 15 }
                });
            }
        }

        /// <summary>
        /// Returns the current status and results of a previously submitted batch geocoding job.
        /// Poll this endpoint after receiving a 202 Accepted from POST.
        /// </summary>
        /// <param name="jobId">Opaque job identifier returned by the submit endpoint.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Job status, progress metrics, and results when completed.</returns>
        [HttpGet("{jobId}/status")]
        [ProducesResponseType(typeof(BatchJob), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStatusAsync(string jobId, CancellationToken ct)
        {
            var job = await _jobStore.GetAsync(jobId, ct);
            return job is null ? NotFound() : Ok(job);
        }

        /// <summary>
        /// Accepts a CSV file upload of addresses and returns geocoding results synchronously.
        /// </summary>
        /// <param name="file">CSV file containing addresses to geocode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of geocoding results with match status, coordinates, and score.</returns>
        [HttpPost("sync")]
        [HttpPost("/api/batchgeocoding/sync")]
        [RequestSizeLimit(MaxUploadBytes)]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Obsolete("Use POST /api/v1/geocoding/batch for the recommended async job pattern.")] // kept for test compatibility
        [ProducesResponseType(typeof(List<BatchGeocodingResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GeocodeAddresses(IFormFile file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { error = "A non-empty CSV file is required." });
            if (!HasCsvExtension(file))
                return BadRequest(new { error = "A .csv file is required." });

            try
            {
                var results = await _batchGeocodingService.GeocodeAsync(file, cancellationToken);
                return Ok(results);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Polly.CircuitBreaker.BrokenCircuitException)
            {
                _logger.LogWarning("ArcGIS circuit breaker is open. Geocoding temporarily unavailable.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
                {
                    Status = 503,
                    Title  = "Geocoding Unavailable",
                    Detail = "The upstream geocoding service is temporarily unavailable. Retry after 15 seconds.",
                    Extensions = { ["retryAfterSeconds"] = 15 }
                });
            }
        }
    }
}

