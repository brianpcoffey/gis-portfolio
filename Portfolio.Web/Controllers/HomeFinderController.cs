using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HomeFinderController : ControllerBase
{
    private readonly IHomeScoringService _scoring;
    private readonly IUserProfileService _profileService;

    public HomeFinderController(IHomeScoringService scoring, IUserProfileService profileService)
    {
        _scoring = scoring;
        _profileService = profileService;
    }

    /// <summary>
    /// Compute and return top 10 properties matching user preferences.
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(List<ScoredPropertyDto>), 200)]
    public async Task<IActionResult> Search(
        [FromBody] HomeSearchPreferencesDto prefs,
        CancellationToken cancellationToken)
    {
        var results = await _scoring.GetTopPropertiesAsync(prefs, 10, cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Get a single property by ID (for popup details).
    /// </summary>
    [HttpGet("property/{id:int}")]
    [ProducesResponseType(typeof(ScoredPropertyDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetProperty(int id, CancellationToken cancellationToken)
    {
        var results = await _scoring.GetTopPropertiesAsync(
            new HomeSearchPreferencesDto { MaxPrice = decimal.MaxValue, MinBedrooms = 0, MinBathrooms = 0, MinSqft = 0, MaxCommuteMin = int.MaxValue },
            int.MaxValue,
            cancellationToken);
        var property = results.FirstOrDefault(p => p.PropertyId == id);
        return property is null ? NotFound() : Ok(property);
    }
}