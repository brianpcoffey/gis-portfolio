using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services
{
    public class SavedFeatureService : ISavedFeatureService
    {
        private readonly ISavedFeatureRepository _repo;

        public SavedFeatureService(ISavedFeatureRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<SavedFeatureDto>> GetAllAsync()
        {
            var features = await _repo.GetAllAsync();
            return features.Select(f => new SavedFeatureDto
            {
                Id = f.Id,
                LayerId = f.LayerId,
                FeatureId = f.FeatureId,
                Name = f.Name,
                GeometryJson = f.GeometryJson,
                DateSaved = f.DateSaved
            }).ToList();
        }

        public async Task<SavedFeatureDto?> GetByIdAsync(int id)
        {
            var f = await _repo.GetByIdAsync(id);
            if (f == null) return null;
            return new SavedFeatureDto
            {
                Id = f.Id,
                LayerId = f.LayerId,
                FeatureId = f.FeatureId,
                Name = f.Name,
                GeometryJson = f.GeometryJson,
                DateSaved = f.DateSaved
            };
        }

        public async Task<SavedFeatureDto> AddAsync(SavedFeatureDto dto)
        {
            var entity = new SavedFeature
            {
                LayerId = dto.LayerId,
                FeatureId = dto.FeatureId,
                Name = dto.Name,
                GeometryJson = dto.GeometryJson,
                DateSaved = DateTime.UtcNow
            };
            var result = await _repo.AddAsync(entity);
            return new SavedFeatureDto
            {
                Id = result.Id,
                LayerId = result.LayerId,
                FeatureId = result.FeatureId,
                Name = result.Name,
                GeometryJson = result.GeometryJson,
                DateSaved = result.DateSaved
            };
        }

        public async Task<SavedFeatureDto> UpdateAsync(SavedFeatureDto dto)
        {
            var entity = await _repo.GetByIdAsync(dto.Id);
            if (entity == null) throw new KeyNotFoundException();
            entity.Name = dto.Name;
            entity.GeometryJson = dto.GeometryJson;
            var result = await _repo.UpdateAsync(entity);
            return new SavedFeatureDto
            {
                Id = result.Id,
                LayerId = result.LayerId,
                FeatureId = result.FeatureId,
                Name = result.Name,
                GeometryJson = result.GeometryJson,
                DateSaved = result.DateSaved
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repo.DeleteAsync(id);
        }
    }
}