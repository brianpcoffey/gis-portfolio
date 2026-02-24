using Portfolio.Common.Models;
using Microsoft.EntityFrameworkCore;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories
{
    public class SavedFeatureRepository : ISavedFeatureRepository
    {
        private readonly PortfolioDbContext _context;
        public SavedFeatureRepository(PortfolioDbContext context) => _context = context;

        public async Task<List<SavedFeature>> GetAllAsync() =>
            await _context.SavedFeatures.Include(f => f.UserNotes).ToListAsync();

        public async Task<SavedFeature?> GetByIdAsync(int id) =>
            await _context.SavedFeatures.Include(f => f.UserNotes).FirstOrDefaultAsync(f => f.Id == id);

        public async Task<SavedFeature> AddAsync(SavedFeature feature)
        {
            _context.SavedFeatures.Add(feature);
            await _context.SaveChangesAsync();
            return feature;
        }

        public async Task<SavedFeature> UpdateAsync(SavedFeature feature)
        {
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