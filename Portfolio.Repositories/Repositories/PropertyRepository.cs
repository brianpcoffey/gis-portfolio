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

    }
}