using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories
{
    public class SavedFeatureRepository : ISavedFeatureRepository
    {
        private readonly PortfolioDbContext _db;
        private readonly ILogger<SavedFeatureRepository> _logger;

        public SavedFeatureRepository(PortfolioDbContext db, ILogger<SavedFeatureRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<List<SavedFeature>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.SavedFeatures
                .Where(sf => sf.UserId == userId)
                .Include(sf => sf.Collection)
                .Include(sf => sf.UserNotes)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<SavedFeature?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.SavedFeatures
                .Include(sf => sf.Collection)
                .Include(sf => sf.UserNotes)
                .FirstOrDefaultAsync(sf => sf.Id == id && sf.UserId == userId, cancellationToken);
        }

        public async Task<SavedFeature?> GetByLayerAndFeatureIdAsync(string layerId, string featureId, Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.SavedFeatures
                .FirstOrDefaultAsync(sf => sf.LayerId == layerId && sf.FeatureId == featureId && sf.UserId == userId, cancellationToken);
        }

        public async Task<SavedFeature?> GetByFeatureKeyAsync(string featureKey, Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.SavedFeatures
                .Include(sf => sf.Collection)
                .Include(sf => sf.UserNotes)
                .FirstOrDefaultAsync(sf => sf.FeatureId == featureKey && sf.UserId == userId, cancellationToken);
        }

        public async Task<SavedFeature> AddAsync(SavedFeature feature, CancellationToken cancellationToken = default)
        {
            _db.SavedFeatures.Add(feature);
            await _db.SaveChangesAsync(cancellationToken);
            await _db.Entry(feature).Reference(f => f.Collection).LoadAsync(cancellationToken);
            await _db.Entry(feature).Collection(f => f.UserNotes).LoadAsync(cancellationToken);
            _logger.LogInformation("SavedFeature {FeatureId} persisted with DB id {Id}", feature.FeatureId, feature.Id);
            return feature;
        }

        public async Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default)
        {
            var sf = await _db.SavedFeatures.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, cancellationToken);
            if (sf == null)
            {
                _logger.LogWarning("SavedFeature DB id {Id} not found for user {UserId}", id, userId);
                return false;
            }
            _db.SavedFeatures.Remove(sf);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> ExistsAsync(string layerId, string featureId, Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.SavedFeatures.AnyAsync(sf => sf.LayerId == layerId && sf.FeatureId == featureId && sf.UserId == userId, cancellationToken);
        }
    }
}