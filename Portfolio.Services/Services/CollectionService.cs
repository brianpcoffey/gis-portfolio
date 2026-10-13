using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Diagnostics;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services
{
    public class CollectionService : ICollectionService
    {
        private readonly ICollectionRepository _repository;
        private readonly IUserProfileService _userProfileService;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CollectionService> _logger;

        public CollectionService(
            ICollectionRepository repository,
            IUserProfileService userProfileService,
            TimeProvider timeProvider,
            ILogger<CollectionService> logger)
        {
            _repository = repository
                ?? throw new ArgumentNullException(nameof(repository));
            _userProfileService = userProfileService
                ?? throw new ArgumentNullException(nameof(userProfileService));
            _timeProvider = timeProvider
                ?? throw new ArgumentNullException(nameof(timeProvider));
            _logger = logger
                ?? throw new ArgumentNullException(nameof(logger));
        }

        // The colour is written into inline CSS by the client, so only #RRGGBB is accepted.
        private static readonly Regex HexColor = new(@"^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

        private static void ValidateColor(string color, string paramName)
        {
            if (!HexColor.IsMatch(color))
                throw new ArgumentException("Color must be a hex value like #6c757d.", paramName);
        }

        private Guid CurrentUserId =>
            _userProfileService.GetCurrentUserId()
                ?? throw new UnauthorizedAccessException("User not identified.");

        public async Task<List<CollectionDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            var items = await _repository.GetAllAsync(CurrentUserId, cancellationToken);
            return items.Select(MapToDto).ToList();
        }

        public async Task<CollectionDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _repository.GetByIdAsync(id, CurrentUserId, cancellationToken);
            return entity == null ? null : MapToDto(entity);
        }

        public async Task<CollectionDto> CreateAsync(
            CollectionCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new ArgumentException("Collection name is required.", nameof(dto));
            if (!string.IsNullOrWhiteSpace(dto.Color))
                ValidateColor(dto.Color.Trim(), nameof(dto));

            var ownerId = CurrentUserId;
            var name = dto.Name.Trim();

            if (await _repository.ExistsAsync(ownerId, name, cancellationToken))
                throw new InvalidOperationException("A collection with this name already exists.");

            var entity = new Collection
            {
                OwnerId = ownerId,
                Name = name,
                Color = string.IsNullOrWhiteSpace(dto.Color) ? "#6c757d" : dto.Color.Trim(),
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            var created = await _repository.AddAsync(entity, cancellationToken);
            _logger.LogInformation("Collection {CollectionId} created for owner {OwnerId}", created.Id, LogRedaction.UserId(ownerId));
            return MapToDto(created);
        }

        public async Task<CollectionDto> UpdateAsync(
            int id,
            CollectionUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            var ownerId = CurrentUserId;
            var entity = await _repository.GetByIdAsync(id, ownerId, cancellationToken)
                ?? throw new KeyNotFoundException($"Collection {id} not found.");

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                var newName = dto.Name.Trim();
                if (!string.Equals(newName, entity.Name, StringComparison.OrdinalIgnoreCase)
                    && await _repository.ExistsAsync(ownerId, newName, cancellationToken))
                {
                    throw new InvalidOperationException("A collection with this name already exists.");
                }
                entity.Name = newName;
            }

            if (!string.IsNullOrWhiteSpace(dto.Color))
            {
                var color = dto.Color.Trim();
                if (!string.Equals(color, entity.Color, StringComparison.Ordinal))
                    ValidateColor(color, nameof(dto));
                entity.Color = color;
            }

            entity.LastModified = _timeProvider.GetUtcNow().UtcDateTime;
            var updated = await _repository.UpdateAsync(entity, cancellationToken);
            _logger.LogInformation("Collection {CollectionId} updated for owner {OwnerId}", id, LogRedaction.UserId(ownerId));
            return MapToDto(updated);
        }

        public async Task<bool> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var ownerId = CurrentUserId;
            var deleted = await _repository.DeleteAsync(id, ownerId, cancellationToken);
            if (deleted)
                _logger.LogInformation("Collection {CollectionId} deleted for owner {OwnerId}", id, LogRedaction.UserId(ownerId));
            else
                _logger.LogWarning("Collection {CollectionId} not found for owner {OwnerId} during delete", id, LogRedaction.UserId(ownerId));
            return deleted;
        }

        private static CollectionDto MapToDto(Collection c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            Color = c.Color,
            CreatedAt = c.CreatedAt,
            LastModified = c.LastModified
        };
    }
}
