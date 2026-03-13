using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Portfolio.Web.Pages.Projects.FiberFlow;

[Authorize]
public class DashboardModel : PageModel
{
    public void OnGet()
    {
    }
}
