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
        var result = await _dashboardService.GetDashboardAsync(cancellationToken);
        return Ok(result);
    }
}
