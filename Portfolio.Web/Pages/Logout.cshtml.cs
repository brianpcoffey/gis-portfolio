using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Portfolio.Web.Pages;

[ValidateAntiForgeryToken]
public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        HttpContext.Response.Cookies.Delete("AnonUserId");

        return RedirectToPage("/Index");
    }
}
