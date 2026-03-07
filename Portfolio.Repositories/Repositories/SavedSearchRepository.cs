using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories
{
    public class SavedSearchRepository : ISavedSearchRepository
    {
        private readonly PortfolioDbContext _db;

        public SavedSearchRepository(PortfolioDbContext db)
        {
            _db = db;
        }

        public async Task<List<SavedSearch>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.SavedSearches
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<SavedSearch?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.SavedSearches
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken);
        }

        public async Task<SavedSearch> AddAsync(SavedSearch savedSearch, CancellationToken cancellationToken = default)
        {
            _db.SavedSearches.Add(savedSearch);
            await _db.SaveChangesAsync(cancellationToken);
            return savedSearch;
        }

        public async Task<SavedSearch> UpdateAsync(SavedSearch savedSearch, CancellationToken cancellationToken = default)
        {
            _db.SavedSearches.Update(savedSearch);
            await _db.SaveChangesAsync(cancellationToken);
            return savedSearch;
        }

        public async Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default)
        {
            var search = await _db.SavedSearches
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken);
            if (search == null) return false;
            _db.SavedSearches.Remove(search);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}