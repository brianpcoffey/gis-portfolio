using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories
{
    public class SavedFeatureRepository : ISavedFeatureRepository
    {
        private readonly PortfolioDbContext _context;

        public SavedFeatureRepository(PortfolioDbContext context)
        {
            _context = context;
        }

        public async Task<bool> ExistsAsync(string layerId, string featureId, CancellationToken cancellationToken = default)
        {
            return await _context.SavedFeatures
                .AnyAsync(f => f.LayerId == layerId && f.FeatureId == featureId, cancellationToken);
        }

        public async Task<SavedFeature> AddAsync(SavedFeature entity)
        {
            await _context.SavedFeatures.AddAsync(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<SavedFeature>> GetAllAsync() =>
            await _context.SavedFeatures.ToListAsync();

        public async Task<SavedFeature?> GetByIdAsync(int id) =>
            await _context.SavedFeatures.FirstOrDefaultAsync(f => f.Id == id);

        public async Task<SavedFeature> UpdateAsync(SavedFeature feature)
        {
            feature.LastModified = DateTime.UtcNow;
            _context.SavedFeatures.Update(feature);
            await _context.SaveChangesAsync();
            return feature;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var feature = await _context.SavedFeatures.FindAsync(id);
            if (feature == null) return false;
            _context.SavedFeatures.Remove(feature);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}