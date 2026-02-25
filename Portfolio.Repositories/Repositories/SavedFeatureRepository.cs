using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories
{
    public class SavedFeatureRepository : ISavedFeatureRepository
    {
        private readonly PortfolioDbContext _db;

        public SavedFeatureRepository(PortfolioDbContext db)
        {
            _db = db;
        }

        public async Task<List<SavedFeature>> GetAllAsync()
        {
            return await _db.SavedFeatures
                .Include(sf => sf.Collection)
                .Include(sf => sf.UserNotes)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<SavedFeature?> GetByIdAsync(int id)
        {
            return await _db.SavedFeatures
                .Include(sf => sf.Collection)
                .Include(sf => sf.UserNotes)
                .FirstOrDefaultAsync(sf => sf.Id == id);
        }

        public async Task<SavedFeature?> GetByLayerAndFeatureIdAsync(string layerId, string featureId)
        {
            return await _db.SavedFeatures
                .FirstOrDefaultAsync(sf => sf.LayerId == layerId && sf.FeatureId == featureId);
        }

        public async Task<SavedFeature?> GetByFeatureKeyAsync(string featureKey)
        {
            return await _db.SavedFeatures
                .Include(sf => sf.Collection)
                .Include(sf => sf.UserNotes)
                .FirstOrDefaultAsync(sf => sf.FeatureId == featureKey);
        }

        public async Task<SavedFeature> AddAsync(SavedFeature feature)
        {
            _db.SavedFeatures.Add(feature);
            await _db.SaveChangesAsync();
            // reload navigation props
            await _db.Entry(feature).Reference(f => f.Collection).LoadAsync();
            await _db.Entry(feature).Collection(f => f.UserNotes).LoadAsync();
            return feature;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var sf = await _db.SavedFeatures.FindAsync(id);
            if (sf == null) return false;
            _db.SavedFeatures.Remove(sf);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}