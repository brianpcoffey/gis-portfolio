using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for Redlands property scoring/search and user-scoped saved searches.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/homefinder")]
    // No class-level [AllowAnonymous]: ASP.NET Core short-circuits authorization when
    // IAllowAnonymous appears anywhere in an endpoint's metadata, so a controller-level
    // one silently neutered the [Authorize] on every saved-search action below (ASP0026).
    // The four genuinely public actions opt out individually instead.
    [EnableRateLimiting("expensive")] // scoring re-runs a DB query + native compute per call
    public class HomeFinderController : ControllerBase
    {
    private readonly IHomeScoringService _scoring;
    private readonly IUserProfileService _profileService;
    private readonly ISavedSearchService _savedSearchService;

    public HomeFinderController(IHomeScoringService scoring, IUserProfileService profileService, ISavedSearchService savedSearchService)
    {
        _scoring = scoring;
        _profileService = profileService;
        _savedSearchService = savedSearchService;
    }

    /// <summary>Compute and return top 10 properties matching user preferences.</summary>
    [AllowAnonymous]
    [HttpPost("search")]
    [ProducesResponseType(typeof(List<ScoredPropertyDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Search([FromBody] HomeSearchPreferencesDto prefs, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var results = await _scoring.GetTopPropertiesAsync(prefs, 10, cancellationToken);
        return Ok(results);
    }

    /// <summary>Backward-compatible alias for computing matching properties.</summary>
    [AllowAnonymous]
    [HttpPost("score")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Obsolete("Use POST /api/v1/homefinder/search.")]
    [ProducesResponseType(typeof(List<ScoredPropertyDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public Task<IActionResult> Score([FromBody] HomeSearchPreferencesDto prefs, CancellationToken cancellationToken)
    {
        return Search(prefs, cancellationToken);
    }

    /// <summary>Get a single property by ID.</summary>
    [AllowAnonymous]
    [HttpGet("property/{id:int}")]
    [ProducesResponseType(typeof(ScoredPropertyDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetProperty(int id, CancellationToken cancellationToken)
    {
        var property = await _scoring.GetPropertyByIdAsync(id, cancellationToken);
        return property is null ? NotFound() : Ok(property);
    }

    /// <summary>Backward-compatible alias for getting a single property by ID.</summary>
    [AllowAnonymous]
    [HttpGet("properties/{id:int}")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Obsolete("Use GET /api/v1/homefinder/property/{id}.")]
    [ProducesResponseType(typeof(ScoredPropertyDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public Task<IActionResult> GetPropertyLegacy(int id, CancellationToken cancellationToken)
    {
        return GetProperty(id, cancellationToken);
    }

    /// <summary>Save a new search to the user's profile.</summary>
    [HttpPost("searches")]
    [Authorize(Policy = "Authenticated")]
    [ProducesResponseType(typeof(SavedSearchDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> SaveSearch([FromBody] SaveSearchRequest request, CancellationToken cancellationToken)
    {
        var userId = _profileService.GetCurrentUserId();
        if (userId is null) return Unauthorized();
        var dto = new CreateSavedSearchDto { Name = request.Name, Preferences = request.Preferences ?? new HomeSearchPreferencesDto(), PropertyIds = request.PropertyIds };
        try
        {
            var saved = await _savedSearchService.CreateSavedSearchAsync(dto, userId.Value, cancellationToken);
            return CreatedAtAction(nameof(GetSearch), new { id = saved.Id }, saved);
        }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    /// <summary>List all saved searches for the current user.</summary>
    [HttpGet("searches")]
    [Authorize(Policy = "Authenticated")]
    [ProducesResponseType(typeof(List<SavedSearchDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetSearches(CancellationToken cancellationToken)
    {
        var userId = _profileService.GetCurrentUserId();
        if (userId is null) return Unauthorized();
        var searches = await _savedSearchService.GetSavedSearchesAsync(userId.Value, cancellationToken);
        return Ok(searches);
    }

    /// <summary>Get a single saved search by ID.</summary>
    [HttpGet("searches/{id:int}")]
    [Authorize(Policy = "Authenticated")]
    [ProducesResponseType(typeof(SavedSearchDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetSearch(int id, CancellationToken cancellationToken)
    {
        var userId = _profileService.GetCurrentUserId();
        if (userId is null) return Unauthorized();
        var searches = await _savedSearchService.GetSavedSearchesAsync(userId.Value, cancellationToken);
        var search = searches.FirstOrDefault(s => s.Id == id);
        return search is null ? NotFound() : Ok(search);
    }

    /// <summary>Delete a saved search.</summary>
    [HttpDelete("searches/{id:int}")]
    [Authorize(Policy = "Authenticated")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DeleteSearch(int id, CancellationToken cancellationToken)
    {
        var userId = _profileService.GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            await _savedSearchService.DeleteSavedSearchAsync(id, userId.Value, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    }
}