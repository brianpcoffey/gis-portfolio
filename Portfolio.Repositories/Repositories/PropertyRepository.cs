using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories
{
    public class PropertyRepository : IPropertyRepository
    {
        private readonly PortfolioDbContext _db;

        public PropertyRepository(PortfolioDbContext db)
        {
            _db = db;
        }

        public async Task<List<Property>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _db.Properties
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<Property?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _db.Properties
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }

        public async Task<List<Property>> GetFilteredAsync(
            decimal maxPrice,
            int minBedrooms,
            int minBathrooms,
            int minSqft,
            int maxCommuteMin,
            CancellationToken cancellationToken = default)
        {
            return await _db.Properties
                .AsNoTracking()
                .Where(p => p.Price <= maxPrice
                         && p.Bedrooms >= minBedrooms
                         && p.Bathrooms >= minBathrooms
                         && p.LotSqft >= minSqft
                         && p.CommuteMin <= maxCommuteMin)
                .ToListAsync(cancellationToken);
        }

        public async Task<Property> AddAsync(Property property, CancellationToken cancellationToken = default)
        {
            _db.Properties.Add(property);
            await _db.SaveChangesAsync(cancellationToken);
            return property;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (property == null) return false;
            _db.Properties.Remove(property);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _db.Properties.AnyAsync(p => p.Id == id, cancellationToken);
        }

        public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            return await _db.Properties.CountAsync(cancellationToken);
        }
    }
}