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

        public async Task<SavedSearch?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _db.SavedSearches
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
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

        /// <summary>
        /// Checks if the user already has a saved search with the given name (case-insensitive).
        /// </summary>
        public async Task<bool> ExistsByNameAsync(Guid userId, string name, CancellationToken cancellationToken = default)
        {
            return await _db.SavedSearches
                .AsNoTracking()
                .AnyAsync(s => s.UserId == userId
                    && s.Name.ToLower() == name.ToLower(), cancellationToken);
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

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var search = await _db.SavedSearches
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (search == null) return false;
            _db.SavedSearches.Remove(search);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}