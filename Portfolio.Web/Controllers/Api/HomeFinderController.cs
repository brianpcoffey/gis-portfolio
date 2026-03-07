using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Services.Interfaces;
using System.Text.Json;

namespace Portfolio.Web.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Authenticated")]
public class HomeFinderController : ControllerBase
{
    private readonly IHomeScoringService _scoring;
    private readonly IUserProfileService _profileService;
    private readonly ISavedSearchService _savedSearchService;

    public HomeFinderController(
        IHomeScoringService scoring,
        IUserProfileService profileService,
        ISavedSearchService savedSearchService)
    {
        _scoring = scoring;
        _profileService = profileService;
        _savedSearchService = savedSearchService;
    }

    /// <summary>
    /// Compute and return top 10 properties matching user preferences.
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(List<ScoredPropertyDto>), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Search(
        [FromBody] HomeSearchPreferencesDto prefs,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

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

    // ===== Saved Searches =====

    /// <summary>
    /// Save a new search to the user's profile.
    /// </summary>
    [HttpPost("searches")]
    [ProducesResponseType(typeof(SavedSearchDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> SaveSearch(
        [FromBody] SaveSearchRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _profileService.GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var entity = new SavedSearch
        {
            UserId = userId.Value,
            Name = request.Name?.Trim() ?? $"Search {DateTime.UtcNow:yyyy-MM-dd}",
            PreferencesJson = JsonSerializer.Serialize(request.Preferences),
            TopPropertyIds = string.Join(",", request.PropertyIds ?? Array.Empty<int>()),
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            var saved = await _savedSearchService.CreateSavedSearchAsync(entity, cancellationToken);
            return CreatedAtAction(nameof(GetSearch), new { id = saved.Id }, ToDto(saved));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// List all saved searches for the current user.
    /// </summary>
    [HttpGet("searches")]
    [ProducesResponseType(typeof(List<SavedSearchDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetSearches(CancellationToken cancellationToken)
    {
        var userId = _profileService.GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var searches = await _savedSearchService.GetSavedSearchesAsync(userId.Value, cancellationToken);
        return Ok(searches.Select(ToDto).ToList());
    }

    /// <summary>
    /// Get a single saved search by ID.
    /// </summary>
    [HttpGet("searches/{id:int}")]
    [ProducesResponseType(typeof(SavedSearchDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetSearch(int id, CancellationToken cancellationToken)
    {
        var userId = _profileService.GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var searches = await _savedSearchService.GetSavedSearchesAsync(userId.Value, cancellationToken);
        var search = searches.FirstOrDefault(s => s.Id == id);
        if (search is null) return NotFound();

        return Ok(ToDto(search));
    }

    /// <summary>
    /// Delete a saved search.
    /// </summary>
    [HttpDelete("searches/{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteSearch(int id, CancellationToken cancellationToken)
    {
        var userId = _profileService.GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            await _savedSearchService.DeleteSavedSearchAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ===== Helpers =====

    private static SavedSearchDto ToDto(SavedSearch s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        CreatedAt = s.CreatedAt,
        Preferences = SafeDeserialize(s.PreferencesJson),
        PropertyIds = s.TopPropertyIds?
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToArray() ?? Array.Empty<int>()
    };

    private static HomeSearchPreferencesDto? SafeDeserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<HomeSearchPreferencesDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }
}