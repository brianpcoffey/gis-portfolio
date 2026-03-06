using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portfolio.Common.Constants;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Areas.Account.Pages
{
    [Authorize(Policy = "Authenticated")]
    public class ProfileModel : PageModel
    {
        private readonly IUserProfileService _profileService;

        public ProfileModel(IUserProfileService profileService)
        {
            _profileService = profileService;
        }

        // --- Read-only fields (from Google) ---
        public Guid UserId { get; private set; }
        public string? Email { get; private set; }
        public string? GoogleName { get; private set; }
        public string? Picture { get; private set; }
        public string? GoogleId { get; private set; }
        public bool IsGoogleLinked { get; private set; }

        // --- Editable fields ---
        [BindProperty]
        public string? DisplayName { get; set; }

        // --- Status ---
        [TempData]
        public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            await LoadProfileDataAsync(cancellationToken);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                await LoadProfileDataAsync(cancellationToken);
                return Page();
            }

            await _profileService.UpdateCurrentProfileAsync(new UpdateProfileDto
            {
                DisplayName = DisplayName
            }, cancellationToken);

            StatusMessage = "Profile updated successfully.";
            return RedirectToPage();
        }

        private async Task LoadProfileDataAsync(CancellationToken cancellationToken)
        {
            var userId = _profileService.GetCurrentUserId();
            if (userId == null) return;

            UserId = userId.Value;
            IsGoogleLinked = await _profileService.IsGoogleLinkedAsync(cancellationToken);

            Email = await _profileService.GetClaimAsync(ProfileClaimTypes.Email, cancellationToken);
            GoogleName = await _profileService.GetClaimAsync(ProfileClaimTypes.Name, cancellationToken);
            Picture = await _profileService.GetClaimAsync(ProfileClaimTypes.Picture, cancellationToken);
            GoogleId = await _profileService.GetClaimAsync(ProfileClaimTypes.GoogleId, cancellationToken);

            // Load editable display name (may be null — user hasn't set one yet)
            DisplayName = await _profileService.GetClaimAsync(ProfileClaimTypes.DisplayName, cancellationToken);
        }
    }
}