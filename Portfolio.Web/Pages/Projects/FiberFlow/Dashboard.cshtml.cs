using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Pages.Projects.FiberFlow;

[Authorize]
public class DashboardModel : PageModel
{
    private readonly IFiberSeedService _seedService;
    public DashboardModel(IFiberSeedService seedService)
    {
        _seedService = seedService;
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await _seedService.UserHasSeedDataAsync(cancellationToken))
        {
            await _seedService.SeedForUserAsync(cancellationToken);
        }
    }
}
