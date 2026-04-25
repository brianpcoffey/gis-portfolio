using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces;

public interface IHomeScoringService
{
    Task<List<ScoredPropertyDto>> GetTopPropertiesAsync(
        HomeSearchPreferencesDto prefs,
        int top = 10,
        CancellationToken cancellationToken = default);
}