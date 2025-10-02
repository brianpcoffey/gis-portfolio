using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Portfolio.Services.Services
{
    public class GisFeatureService : IGisFeatureService
    {
        private readonly IGisFeatureRepository _repo;
        public GisFeatureService(IGisFeatureRepository repo) => _repo = repo;

        public async Task<List<GisFeatureDto>> GetAllAsync()
        {
            var features = await _repo.GetAllAsync();
            return features.Select(f => new GisFeatureDto
            {
                Id = f.Id,
                Name = f.Name,
                Description = f.Description,
                FeatureType = f.FeatureType,
                Coordinates = f.Coordinates
            }).ToList();
        }

        public async Task<GisFeatureDto?> GetByIdAsync(int id)
        {
            var f = await _repo.GetByIdAsync(id);
            if (f == null) return null;
            return new GisFeatureDto
            {
                Id = f.Id,
                Name = f.Name,
                Description = f.Description,
                FeatureType = f.FeatureType,
                Coordinates = f.Coordinates
            };
        }

        public async Task<GisFeatureDto> AddAsync(GisFeatureDto dto, string userName)
        {
            // Add validation/business rules here
            var feature = new GisFeature
            {
                Name = dto.Name,
                Description = dto.Description,
                FeatureType = dto.FeatureType,
                Coordinates = dto.Coordinates,
                CreatedBy = userName
            };
            var result = await _repo.AddAsync(feature);
            return new GisFeatureDto
            {
                Id = result.Id,
                Name = result.Name,
                Description = result.Description,
                FeatureType = result.FeatureType,
                Coordinates = result.Coordinates
            };
        }

        public async Task<GisFeatureDto> UpdateAsync(GisFeatureDto dto, string userName)
        {
            var feature = await _repo.GetByIdAsync(dto.Id);
            if (feature == null) throw new KeyNotFoundException();
            // Add authorization check for userName if needed
            feature.Name = dto.Name;
            feature.Description = dto.Description;
            feature.FeatureType = dto.FeatureType;
            feature.Coordinates = dto.Coordinates;
            var result = await _repo.UpdateAsync(feature);
            return new GisFeatureDto
            {
                Id = result.Id,
                Name = result.Name,
                Description = result.Description,
                FeatureType = result.FeatureType,
                Coordinates = result.Coordinates
            };
        }

        public async Task<bool> DeleteAsync(int id, string userName)
        {
            // Add authorization check for userName if needed
            return await _repo.DeleteAsync(id);
        }
    }
}
