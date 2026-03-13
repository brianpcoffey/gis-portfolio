using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Portfolio.Web.Pages.Projects.FiberFlow;

[Authorize]
public class InventoryModel : PageModel
{
    public void OnGet()
    {
    }
}
