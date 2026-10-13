using System.Security.Cryptography;
using System.Text;

namespace Portfolio.Repositories.Diagnostics;

/// <summary>Helpers that keep personal identifiers out of log output.</summary>
/// <remarks>
/// Warning-level logs are kept in production, so a raw user GUID there would be a durable, correlatable identifier.
/// </remarks>
public static class LogRedaction
{
    /// <summary>
    /// Returns a stable, non-reversible tag for a user id (the first 8 lowercase hex characters of the SHA-256 of the
    /// id's canonical lowercase "D" string, UTF-8), so the same user always yields the same tag and log lines can still
    /// be correlated without exposing the id.
    /// </summary>
    public static string UserId(Guid userId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userId.ToString("D")));
        return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
    }

    /// <summary>
    /// Returns <paramref name="value"/> with every line break (CR, LF, NEL, U+2028, U+2029) replaced by a space, so a
    /// client-supplied value (a request path, a feature id, a claim type) cannot start a forged log line. Null stays null.
    /// </summary>
    public static string? SingleLine(string? value) =>
        value?.Replace("\r", " ").Replace("\n", " ").Replace("\u0085", " ").Replace("\u2028", " ").Replace("\u2029", " ");
}
