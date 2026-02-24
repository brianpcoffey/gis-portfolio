using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services
{
    public class SavedFeatureService : ISavedFeatureService
    {
        private readonly ISavedFeatureRepository _repository;

        public SavedFeatureService(ISavedFeatureRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<SavedFeatureDto>> GetAllAsync()
        {
            var features = await _repository.GetAllAsync();
            return features.Select(MapToDto).ToList();
        }

        public async Task<SavedFeatureDto?> GetByIdAsync(int id)
        {
            var feature = await _repository.GetByIdAsync(id);
            return feature is null ? null : MapToDto(feature);
        }

        public async Task<SavedFeatureDto> AddAsync(SavedFeatureDto dto)
        {
            var entity = new SavedFeature
            {
                LayerId = dto.LayerId,
                FeatureId = dto.FeatureId,
                Name = dto.Name,
                GeometryJson = dto.GeometryJson,
                Description = dto.Description,
                DateSaved = DateTime.UtcNow
            };
            var result = await _repository.AddAsync(entity);
            return MapToDto(result);
        }

        public async Task<SavedFeatureDto> UpdateAsync(SavedFeatureDto dto)
        {
            var entity = await _repository.GetByIdAsync(dto.Id)
                ?? throw new KeyNotFoundException($"Feature with ID {dto.Id} not found.");

            entity.Name = dto.Name;
            entity.GeometryJson = dto.GeometryJson;
            entity.Description = dto.Description;
            entity.LastModified = DateTime.UtcNow;

            var result = await _repository.UpdateAsync(entity);
            return MapToDto(result);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }

        public async Task<SavedFeatureDto> CreateAsync(SavedFeatureCreateDto dto, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(dto.LayerId, nameof(dto.LayerId));
            ArgumentException.ThrowIfNullOrWhiteSpace(dto.FeatureId, nameof(dto.FeatureId));
            ArgumentException.ThrowIfNullOrWhiteSpace(dto.Name, nameof(dto.Name));
            ArgumentException.ThrowIfNullOrWhiteSpace(dto.GeometryJson, nameof(dto.GeometryJson));

            var exists = await _repository.ExistsAsync(dto.LayerId, dto.FeatureId, cancellationToken);
            if (exists)
                throw new InvalidOperationException("Feature already saved.");

            var entity = new SavedFeature
            {
                LayerId = dto.LayerId,
                FeatureId = dto.FeatureId,
                Name = dto.Name,
                GeometryJson = dto.GeometryJson,
                Description = dto.Description,
                DateSaved = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            var result = await _repository.AddAsync(entity);
            return MapToDto(result);
        }

        private static SavedFeatureDto MapToDto(SavedFeature entity) => new()
        {
            Id = entity.Id,
            LayerId = entity.LayerId,
            FeatureId = entity.FeatureId,
            Name = entity.Name,
            GeometryJson = entity.GeometryJson,
            Description = entity.Description,
            DateSaved = entity.DateSaved,
            LastModified = entity.LastModified
        };
    }
}