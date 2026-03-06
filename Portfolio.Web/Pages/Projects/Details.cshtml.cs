using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Pages.Projects;

[Authorize(Policy = "Authenticated")]
public class DetailsModel : PageModel
{
    private readonly IUserProfileService _userProfileService;

    public DetailsModel(IUserProfileService userProfileService)
    {
        _userProfileService = userProfileService;
    }

    public async Task<IActionResult> OnGetAsync(string projectId, CancellationToken cancellationToken)
    {
        // The UserId is guaranteed available for authenticated users
        var userId = _userProfileService.GetCurrentUserId();

        // Load project, features, etc. using userId to scope data
        // ...

        return Page();
    }
}