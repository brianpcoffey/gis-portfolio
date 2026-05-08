using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Portfolio.Web.Pages.Projects.Network
{
    [AllowAnonymous]
    public class DetailsModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
