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

    public Guid UserId { get; private set; }

    public async Task<IActionResult> OnGetAsync(string projectId, CancellationToken cancellationToken)
    {
        var userId = _userProfileService.GetCurrentUserId()
            ?? throw new InvalidOperationException("User not available.");

        UserId = userId;

        // TODO: Load project data by projectId scoped to userId
        return Page();
    }
}