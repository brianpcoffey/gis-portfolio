using Microsoft.AspNetCore.Mvc;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class FiberDashboardController : ControllerBase
{
    private readonly IFiberDashboardService _dashboardService;
    public FiberDashboardController(IFiberDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _dashboardService.GetDashboardAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            // Log the error (for now, return details in response)
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }
}
