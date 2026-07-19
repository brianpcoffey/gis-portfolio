using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;
using System.Text.Json;

namespace Portfolio.Services.Services
{
    public class SavedSearchService : ISavedSearchService
    {
        private readonly ISavedSearchRepository _repo;
        private readonly TimeProvider _timeProvider;

        public SavedSearchService(ISavedSearchRepository repo, TimeProvider timeProvider)
        {
            _repo = repo;
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Creates a saved search after validating that the name is unique per user.
        /// </summary>
        public async Task<SavedSearchDto> CreateSavedSearchAsync(CreateSavedSearchDto dto, Guid userId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new ArgumentException("Name is required.", nameof(dto));

            var name = dto.Name.Trim();

            var duplicate = await _repo.ExistsByNameAsync(userId, name, cancellationToken);
            if (duplicate)
                throw new InvalidOperationException($"A saved search with the name '{name}' already exists.");

            var entity = new SavedSearch
            {
                UserId = userId,
                Name = name,
                PreferencesJson = JsonSerializer.Serialize(dto.Preferences),
                TopPropertyIds = string.Join(",", dto.PropertyIds ?? Array.Empty<int>()),
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            var saved = await _repo.AddAsync(entity, cancellationToken);
            return MapToDto(saved);
        }

        /// <summary>
        /// Returns all saved searches for a given user, ordered by most recent.
        /// </summary>
        public async Task<List<SavedSearchDto>> GetSavedSearchesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var entities = await _repo.GetAllAsync(userId, cancellationToken);
            return entities.Select(MapToDto).ToList();
        }

        /// <summary>
        /// Deletes a saved search by ID, scoped to its owner so a user can only delete
        /// their own searches. Throws KeyNotFoundException if the search does not exist
        /// or belongs to another user (indistinguishable by design, to avoid disclosing
        /// the existence of other users' records).
        /// </summary>
        public async Task DeleteSavedSearchAsync(int id, Guid userId, CancellationToken cancellationToken = default)
        {
            var deleted = await _repo.DeleteAsync(id, userId, cancellationToken);
            if (!deleted)
                throw new KeyNotFoundException($"Saved search with ID {id} was not found.");
        }

        private static SavedSearchDto MapToDto(SavedSearch s) => new()
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

        // Cached — allocating JsonSerializerOptions per call defeats System.Text.Json's
        // internal metadata cache.
        private static readonly JsonSerializerOptions DeserializeOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static HomeSearchPreferencesDto? SafeDeserialize(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<HomeSearchPreferencesDto>(json, DeserializeOptions);
            }
            catch
            {
                return null;
            }
        }
    }
}