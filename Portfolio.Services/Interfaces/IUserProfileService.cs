using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Resolves the current user's identity and manages their profile, claims, and Google account link.
    /// </summary>
    public interface IUserProfileService
    {
        // --- Identity resolution ---

        /// <summary>Reads the current user's id from the AnonUserId cookie or Google identity.</summary>
        /// <returns>The current user's id, or <c>null</c> if none can be resolved.</returns>
        Guid? GetCurrentUserId();

        // --- Claims CRUD (scoped to current user) ---

        /// <summary>Returns all claims for the current user.</summary>
        Task<List<ClaimDto>> GetClaimsAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns the value of the current user's claim of the given type, or <c>null</c> if not set.</summary>
        Task<string?> GetClaimAsync(string type, CancellationToken cancellationToken = default);

        /// <summary>Sets (creates or replaces) a claim of the given type on the current user.</summary>
        Task SetClaimAsync(string type, string value, CancellationToken cancellationToken = default);

        /// <summary>Removes the claim of the given type from the current user.</summary>
        /// <returns><c>true</c> if a claim was removed; otherwise <c>false</c>.</returns>
        Task<bool> RemoveClaimAsync(string type, CancellationToken cancellationToken = default);

        // --- Profile CRUD ---

        /// <summary>Returns the profile with the given user id, or <c>null</c> if not found.</summary>
        Task<ProfileDto?> GetProfileByIdAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Returns the profile linked to the given Google id, or <c>null</c> if not found.</summary>
        Task<ProfileDto?> GetProfileByGoogleIdAsync(string googleId, CancellationToken cancellationToken = default);

        /// <summary>Returns the current user's profile.</summary>
        Task<ProfileDto> GetCurrentProfileAsync(CancellationToken cancellationToken = default);

        /// <summary>Updates the current user's profile and returns the updated record.</summary>
        Task<ProfileDto> UpdateCurrentProfileAsync(UpdateProfileDto dto, CancellationToken cancellationToken = default);

        /// <summary>Deletes the profile with the given user id.</summary>
        /// <returns><c>true</c> if a row was deleted; otherwise <c>false</c>.</returns>
        Task<bool> DeleteProfileAsync(Guid userId, CancellationToken cancellationToken = default);

        // --- Google OAuth integration ---

        /// <summary>Creates or updates a profile from a Google identity and returns the resulting user id.</summary>
        Task<Guid> CreateOrUpdateFromGoogleAsync(GoogleProfileDto google, CancellationToken cancellationToken = default);

        // --- Convenience helpers ---

        /// <summary>
        /// Returns true if the current user has a linked Google account.
        /// </summary>
        Task<bool> IsGoogleLinkedAsync(CancellationToken cancellationToken = default);
    }
}