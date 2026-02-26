using Microsoft.AspNetCore.Mvc.RazorPages;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Areas.Account.Pages
{
    public class ProfileModel : PageModel
    {
        private readonly IUserProfileService _profileService;

        public ProfileModel(IUserProfileService profileService) => _profileService = profileService;

        public Guid? UserId { get; private set; }
        public List<(string Type, string Value)> Claims { get; private set; } = new();

        public async Task OnGetAsync()
        {
            UserId = _profileService.GetCurrentUserId();
            var claims = await _profileService.GetClaimsAsync();
            foreach (var c in claims) Claims.Add((c.Type, c.Value));
        }
    }
}