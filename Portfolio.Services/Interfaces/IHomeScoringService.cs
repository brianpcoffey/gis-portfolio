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

    /// <summary>
    /// Returns the property with the given id, or <c>null</c> if not found. The result is <b>not</b> scored: it carries
    /// the listing fields and <c>EstimatedMonthlyCost</c>, while the sub-scores, <c>CompositeScore</c> and <c>Rank</c>
    /// are left at 0 (scoring needs preferences; use <see cref="GetTopPropertiesAsync"/> for ranked results).
    /// </summary>
    Task<ScoredPropertyDto?> GetPropertyByIdAsync(int id, CancellationToken cancellationToken = default);
}
