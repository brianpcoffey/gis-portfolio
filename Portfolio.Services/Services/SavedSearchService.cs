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

        public SavedSearchService(ISavedSearchRepository repo)
        {
            _repo = repo;
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
                CreatedAt = DateTime.UtcNow
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
        /// Deletes a saved search by ID. Throws if not found.
        /// </summary>
        public async Task DeleteSavedSearchAsync(int id, CancellationToken cancellationToken = default)
        {
            var exists = await _repo.GetByIdAsync(id, cancellationToken);
            if (exists is null)
                throw new KeyNotFoundException($"Saved search with ID {id} was not found.");

            await _repo.DeleteAsync(id, cancellationToken);
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
}