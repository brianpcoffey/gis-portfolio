using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoint for aggregated fiber operations dashboard KPIs (revenue, open orders,
    /// active shipments, and inventory alerts) scoped to the authenticated user.
    /// </summary>
    [Authorize(Policy = "Authenticated")]
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/fiber/dashboard")]
    public class FiberDashboardController : ControllerBase
    {
    private readonly IFiberDashboardService _dashboardService;

    public FiberDashboardController(IFiberDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>Retrieves fiber dashboard statistics.</summary>
    [HttpGet("stats")]
    [ProducesResponseType(200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetDashboardAsync(cancellationToken);
        return Ok(result);
    }

}
}
