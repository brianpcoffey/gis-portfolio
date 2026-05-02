using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api;

[Authorize(Policy = "Authenticated")]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/fiber/dashboard")]
public class FiberDashboardController : ControllerBase
{
    private readonly IFiberDashboardService _dashboardService;
    private readonly ILogger<FiberDashboardController> _logger;

    public FiberDashboardController(IFiberDashboardService dashboardService, ILogger<FiberDashboardController> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    /// <summary>Retrieves fiber dashboard statistics.</summary>
    [HttpGet("stats")]
    [ProducesResponseType(200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _dashboardService.GetDashboardAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in {Controller}.{Action}.",
                nameof(FiberDashboardController), nameof(GetStats));
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = 500,
                Title  = "An unexpected error occurred.",
                Detail = "See server logs for details."
            });
        }
    }
}
