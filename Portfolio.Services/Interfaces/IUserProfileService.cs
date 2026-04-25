using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    public interface IUserProfileService
    {
        // --- Identity resolution ---
        Guid? GetCurrentUserId();

        // --- Claims CRUD (scoped to current user) ---
        Task<List<ClaimDto>> GetClaimsAsync(CancellationToken cancellationToken = default);
        Task<string?> GetClaimAsync(string type, CancellationToken cancellationToken = default);
        Task SetClaimAsync(string type, string value, CancellationToken cancellationToken = default);
        Task<bool> RemoveClaimAsync(string type, CancellationToken cancellationToken = default);

        // --- Profile CRUD ---
        Task<ProfileDto?> GetProfileByIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<ProfileDto?> GetProfileByGoogleIdAsync(string googleId, CancellationToken cancellationToken = default);
        Task<ProfileDto> GetCurrentProfileAsync(CancellationToken cancellationToken = default);
        Task<ProfileDto> UpdateCurrentProfileAsync(UpdateProfileDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteProfileAsync(Guid userId, CancellationToken cancellationToken = default);

        // --- Google OAuth integration ---
        Task<Guid> CreateOrUpdateFromGoogleAsync(GoogleProfileDto google, CancellationToken cancellationToken = default);

        // --- Convenience helpers ---
        /// <summary>
        /// Returns true if the current user has a linked Google account.
        /// </summary>
        Task<bool> IsGoogleLinkedAsync(CancellationToken cancellationToken = default);
    }
}