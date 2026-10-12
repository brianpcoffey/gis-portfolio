using System.Security.Cryptography;
using System.Text;

namespace Portfolio.Repositories.Diagnostics;

/// <summary>
/// Helpers that keep personal identifiers out of log output. Warning-level logs are kept in
/// production, so a raw user GUID there would be a durable, correlatable identifier.
/// </summary>
public static class LogRedaction
{
    /// <summary>
    /// Returns a stable, non-reversible tag for a user id: the first 8 lowercase hex characters of
    /// the SHA-256 of the id's canonical lowercase "D" string (UTF-8). The same user always yields
    /// the same tag, so log lines can still be correlated without exposing the id.
    /// </summary>
    public static string UserId(Guid userId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userId.ToString("D")));
        return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
    }
}
