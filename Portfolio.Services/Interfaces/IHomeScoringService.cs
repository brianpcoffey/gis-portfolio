using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces;

/// <summary>
/// Ranks Redlands properties against weighted home-search preferences.
/// </summary>
public interface IHomeScoringService
{
    /// <summary>Returns up to <paramref name="top"/> properties ranked against the supplied preferences.</summary>
    Task<List<ScoredPropertyDto>> GetTopPropertiesAsync(
        HomeSearchPreferencesDto prefs,
        int top = 10,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the scored property with the given id, or <c>null</c> if not found.</summary>
    Task<ScoredPropertyDto?> GetPropertyByIdAsync(int id, CancellationToken cancellationToken = default);
}
