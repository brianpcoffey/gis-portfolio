using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    public interface IUserProfileService
    {
        Guid? GetCurrentUserId();
        Task<List<ClaimDto>> GetClaimsAsync(CancellationToken cancellationToken = default);
        Task<string?> GetClaimAsync(string type, CancellationToken cancellationToken = default);
        Task SetClaimAsync(string type, string value, CancellationToken cancellationToken = default);
        Task<bool> RemoveClaimAsync(string type, CancellationToken cancellationToken = default);
    }
}