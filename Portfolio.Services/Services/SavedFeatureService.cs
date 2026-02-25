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

        public SavedFeatureService(ISavedFeatureRepository repo, IUserNoteRepository noteRepo)
        {
            _repo = repo;
            _noteRepo = noteRepo;
        }

        public async Task<List<SavedFeatureDto>> GetAllAsync()
        {
            var list = await _repo.GetAllAsync();
            return list.Select(MapToDto).ToList();
        }

        public async Task<SavedFeatureDto> CreateAsync(CreateSavedFeatureDto dto)
        {
            // ensure not duplicate
            var existing = await _repo.GetByLayerAndFeatureIdAsync(dto.LayerId, dto.FeatureId);
            if (existing != null)
            {
                throw new InvalidOperationException("Feature already saved");
            }

            var entity = new SavedFeature
            {
                LayerId = dto.LayerId,
                FeatureId = dto.FeatureId,
                Name = dto.Name ?? string.Empty,
                GeometryJson = dto.GeometryJson ?? string.Empty,
                Description = dto.Description,
                CollectionId = dto.CollectionId,
                DateSaved = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            var saved = await _repo.AddAsync(entity);

            if (!string.IsNullOrWhiteSpace(dto.Description))
            {
                var note = new UserNote
                {
                    SavedFeatureId = saved.Id,
                    Note = dto.Description,
                    CreatedAt = DateTime.UtcNow
                };
                await _noteRepo.AddAsync(note);
            }

            return MapToDto(saved);
        }

        public async Task<bool> DeleteByDbIdAsync(int id)
        {
            return await _repo.DeleteAsync(id);
        }

        public async Task<bool> DeleteByFeatureKeyAsync(string featureKey)
        {
            var sf = await _repo.GetByFeatureKeyAsync(featureKey);
            if (sf == null) return false;
            return await _repo.DeleteAsync(sf.Id);
        }

        private SavedFeatureDto MapToDto(SavedFeature sf)
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