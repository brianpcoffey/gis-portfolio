using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class LoginModel : PageModel
{
    public IActionResult OnGet(string handler)
    {
        if (handler == "Google")
        {
            var redirectUrl = Url.Content("~/");
            return Challenge(new AuthenticationProperties { RedirectUri = redirectUrl }, "Google");
        }
        return Page();
    }
}
