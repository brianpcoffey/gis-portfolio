using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Diagnostics;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services
{
    public class SavedFeatureService : ISavedFeatureService
    {
        private readonly ISavedFeatureRepository _repo;
        private readonly ICollectionRepository _collectionRepo;
        private readonly IUserProfileService _userProfileService;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SavedFeatureService> _logger;

        public SavedFeatureService(
            ISavedFeatureRepository repo,
            ICollectionRepository collectionRepo,
            IUserProfileService userProfileService,
            TimeProvider timeProvider,
            ILogger<SavedFeatureService> logger)
        {
            _repo = repo;
            _collectionRepo = collectionRepo;
            _userProfileService = userProfileService;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        // Write-side limits. Only Name mirrors its column (200); the LayerId/FeatureId columns are unbounded,
        // so those caps exist only here. GeometryJson is 2x the largest
        // Census state polygon measured from the State Explorer feature read (85,516), floored at 1 MB.
        private const int MaxIdLength = 64;
        private const int MaxNameLength = 200;
        private const int MaxDescriptionLength = 2000;
        private const int MaxGeometryJsonLength = 1_000_000;

        private Guid CurrentUserId =>
            _userProfileService.GetCurrentUserId()
                ?? throw new InvalidOperationException("User not identified");

        public async Task<List<SavedFeatureDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var userId = _userProfileService.GetCurrentUserId();
            if (userId is null)
            {
                _logger.LogWarning("GetAllAsync called with no resolved user identity.");
                throw new InvalidOperationException("No current user identity is available.");
            }
            var list = await _repo.GetAllAsync(userId.Value, cancellationToken);
            return list.Select(MapToDto).ToList();
        }

        public async Task<SavedFeatureDto> CreateAsync(CreateSavedFeatureDto dto, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(dto.LayerId))
                throw new ArgumentException("LayerId is required", nameof(dto));
            if (string.IsNullOrWhiteSpace(dto.FeatureId))
                throw new ArgumentException("FeatureId is required", nameof(dto));
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new ArgumentException("Name is required", nameof(dto));
            if (string.IsNullOrWhiteSpace(dto.GeometryJson))
                throw new ArgumentException("GeometryJson is required", nameof(dto));

            if (dto.LayerId.Length > MaxIdLength)
                throw new ArgumentException($"LayerId must be {MaxIdLength} characters or fewer.", nameof(dto));
            if (dto.FeatureId.Length > MaxIdLength)
                throw new ArgumentException($"FeatureId must be {MaxIdLength} characters or fewer.", nameof(dto));
            if (dto.Name.Length > MaxNameLength)
                throw new ArgumentException($"Name must be {MaxNameLength} characters or fewer.", nameof(dto));
            if (dto.Description is { Length: > MaxDescriptionLength })
                throw new ArgumentException($"Description must be {MaxDescriptionLength} characters or fewer.", nameof(dto));
            if (dto.GeometryJson.Length > MaxGeometryJsonLength)
                throw new ArgumentException($"GeometryJson must be {MaxGeometryJsonLength} characters or fewer.", nameof(dto));

            var userId = CurrentUserId;

            // Validate the target collection (if any) exists and belongs to this user,
            // so a stale/foreign CollectionId returns a clean 400 rather than a DB
            // foreign-key violation surfacing as a 500.
            if (dto.CollectionId.HasValue)
            {
                var collection = await _collectionRepo.GetByIdAsync(dto.CollectionId.Value, userId, cancellationToken);
                if (collection is null)
                    throw new ArgumentException(
                        "The specified collection does not exist or is not owned by the current user.", nameof(dto));
            }

            var existing = await _repo.GetByLayerAndFeatureIdAsync(dto.LayerId, dto.FeatureId, userId, cancellationToken);
            if (existing != null)
                throw new InvalidOperationException("Feature already saved");

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var entity = new SavedFeature
            {
                UserId = userId,
                LayerId = dto.LayerId,
                FeatureId = dto.FeatureId,
                Name = dto.Name ?? string.Empty,
                GeometryJson = dto.GeometryJson ?? string.Empty,
                Description = dto.Description,
                CollectionId = dto.CollectionId,
                DateSaved = now,
                LastModified = now
            };

            var saved = await _repo.AddAsync(entity, cancellationToken);
            _logger.LogInformation("Feature {FeatureId} (layer {LayerId}) saved for user {UserId}",
                LogRedaction.SingleLine(saved.FeatureId), LogRedaction.SingleLine(saved.LayerId), LogRedaction.UserId(userId));
            return MapToDto(saved);
        }

        public async Task<bool> DeleteByDbIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var userId = CurrentUserId;
            var deleted = await _repo.DeleteAsync(id, userId, cancellationToken);
            if (!deleted)
                _logger.LogWarning("Saved feature DB id {Id} not found for user {UserId} during delete", id, LogRedaction.UserId(userId));
            return deleted;
        }

        public async Task<bool> DeleteByFeatureKeyAsync(string featureKey, CancellationToken cancellationToken = default)
        {
            var userId = CurrentUserId;
            var sf = await _repo.GetByFeatureKeyAsync(featureKey, userId, cancellationToken);
            if (sf == null)
            {
                _logger.LogWarning("Saved feature key {FeatureKey} not found for user {UserId} during delete",
                    LogRedaction.SingleLine(featureKey), LogRedaction.UserId(userId));
                return false;
            }
            return await _repo.DeleteAsync(sf.Id, userId, cancellationToken);
        }

        private static SavedFeatureDto MapToDto(SavedFeature sf) => new()
        {
            Id = sf.Id,
            LayerId = sf.LayerId,
            FeatureId = sf.FeatureId,
            Name = sf.Name,
            GeometryJson = sf.GeometryJson,
            Description = sf.Description,
            CollectionId = sf.CollectionId,
            CollectionName = sf.Collection?.Name,
            DateSaved = sf.DateSaved,
            LastModified = sf.LastModified
        };
    }
}
