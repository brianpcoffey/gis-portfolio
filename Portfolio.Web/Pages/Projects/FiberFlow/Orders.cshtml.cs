using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Portfolio.Web.Pages.Projects.FiberFlow;

[Authorize]
public class OrdersModel : PageModel
{
    public void OnGet()
    {
    }
}
