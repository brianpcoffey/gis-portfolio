using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Portfolio.Web.Pages.Projects.ReverseGeocoding
{
    [AllowAnonymous]
    public class DetailsModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
