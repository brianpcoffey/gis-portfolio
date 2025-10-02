using Portfolio.Common.Models;
using Microsoft.EntityFrameworkCore;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Services
{
    public class GisFeatureRepository : IGisFeatureRepository
    {
        private readonly GisDbContext _context;
        public GisFeatureRepository(GisDbContext context) => _context = context;

        public async Task<List<GisFeature>> GetAllAsync() => await _context.GisFeatures.ToListAsync();
        public async Task<GisFeature?> GetByIdAsync(int id) => await _context.GisFeatures.FindAsync(id);
        public async Task<GisFeature> AddAsync(GisFeature feature)
        {
            _context.GisFeatures.Add(feature);
            await _context.SaveChangesAsync();
            return feature;
        }
        public async Task<GisFeature> UpdateAsync(GisFeature feature)
        {
            _context.GisFeatures.Update(feature);
            await _context.SaveChangesAsync();
            return feature;
        }
        public async Task<bool> DeleteAsync(int id)
        {
            var feature = await _context.GisFeatures.FindAsync(id);
            if (feature == null) return false;
            _context.GisFeatures.Remove(feature);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
