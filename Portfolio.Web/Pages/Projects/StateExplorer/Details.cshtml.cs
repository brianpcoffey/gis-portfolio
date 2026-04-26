using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Portfolio.Web.Pages.Projects.StateExplorer
{
    [AllowAnonymous]
    public class DetailsModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
