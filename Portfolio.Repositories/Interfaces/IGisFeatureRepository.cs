using Portfolio.Common.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Portfolio.Repositories.Interfaces
{
    public interface IGisFeatureRepository
    {
        Task<List<GisFeature>> GetAllAsync();
        Task<GisFeature?> GetByIdAsync(int id);
        Task<GisFeature> AddAsync(GisFeature feature);
        Task<GisFeature> UpdateAsync(GisFeature feature);
        Task<bool> DeleteAsync(int id);
    }
}
