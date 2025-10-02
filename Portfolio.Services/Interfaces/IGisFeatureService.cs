using Portfolio.Common.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Portfolio.Services.Interfaces
{
    public interface IGisFeatureService
    {
        Task<List<GisFeatureDto>> GetAllAsync();
        Task<GisFeatureDto?> GetByIdAsync(int id);
        Task<GisFeatureDto> AddAsync(GisFeatureDto dto, string userName);
        Task<GisFeatureDto> UpdateAsync(GisFeatureDto dto, string userName);
        Task<bool> DeleteAsync(int id, string userName);
    }
}
