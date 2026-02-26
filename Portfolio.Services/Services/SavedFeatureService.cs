using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services
{
    public class SavedFeatureService : ISavedFeatureService
    {
        private readonly ISavedFeatureRepository _repo;
        private readonly IUserNoteRepository _noteRepo;
        private readonly IUserProfileService _userProfileService;

        public SavedFeatureService(
            ISavedFeatureRepository repo,
            IUserNoteRepository noteRepo,
            IUserProfileService userProfileService)
        {
            _repo = repo;
            _noteRepo = noteRepo;
            _userProfileService = userProfileService;
        }

        private Guid CurrentUserId =>
            _userProfileService.GetCurrentUserId()
                ?? throw new InvalidOperationException("User not identified");

        public async Task<List<SavedFeatureDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var list = await _repo.GetAllAsync(CurrentUserId, cancellationToken);
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

            var userId = CurrentUserId;

            var existing = await _repo.GetByLayerAndFeatureIdAsync(dto.LayerId, dto.FeatureId, userId, cancellationToken);
            if (existing != null)
                throw new InvalidOperationException("Feature already saved");

            var entity = new SavedFeature
            {
                UserId = userId,
                LayerId = dto.LayerId,
                FeatureId = dto.FeatureId,
                Name = dto.Name ?? string.Empty,
                GeometryJson = dto.GeometryJson ?? string.Empty,
                Description = dto.Description,
                CollectionId = dto.CollectionId,
                DateSaved = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            var saved = await _repo.AddAsync(entity, cancellationToken);

            if (!string.IsNullOrWhiteSpace(dto.Description))
            {
                var note = new UserNote
                {
                    UserId = userId,
                    SavedFeatureId = saved.Id,
                    Note = dto.Description,
                    CreatedAt = DateTime.UtcNow
                };
                await _noteRepo.AddAsync(note, cancellationToken);
            }

            return MapToDto(saved);
        }

        public async Task<bool> DeleteByDbIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _repo.DeleteAsync(id, CurrentUserId, cancellationToken);
        }

        public async Task<bool> DeleteByFeatureKeyAsync(string featureKey, CancellationToken cancellationToken = default)
        {
            var sf = await _repo.GetByFeatureKeyAsync(featureKey, CurrentUserId, cancellationToken);
            if (sf == null) return false;
            return await _repo.DeleteAsync(sf.Id, CurrentUserId, cancellationToken);
        }

        private static SavedFeatureDto MapToDto(SavedFeature sf)
        {
            return new SavedFeatureDto
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
}