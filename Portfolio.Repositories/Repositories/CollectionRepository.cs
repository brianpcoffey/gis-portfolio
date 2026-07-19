using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories
{
    public class CollectionRepository : ICollectionRepository
    {
        private readonly PortfolioDbContext _db;
        private readonly ILogger<CollectionRepository> _logger;

        public CollectionRepository(PortfolioDbContext db, ILogger<CollectionRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<List<Collection>> GetAllAsync(Guid ownerId, CancellationToken cancellationToken = default)
        {
            return await _db.Collections
                .AsNoTracking()
                .Where(c => c.OwnerId == ownerId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<Collection?> GetByIdAsync(int id, Guid ownerId, CancellationToken cancellationToken = default)
        {
            return await _db.Collections
                .FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == ownerId, cancellationToken);
        }

        public async Task<Collection> AddAsync(Collection entity, CancellationToken cancellationToken = default)
        {
            _db.Collections.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Collection {CollectionId} added", entity.Id);
            return entity;
        }

        public async Task<Collection> UpdateAsync(Collection entity, CancellationToken cancellationToken = default)
        {
            _db.Collections.Update(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return entity;
        }

        public async Task<bool> DeleteAsync(int id, Guid ownerId, CancellationToken cancellationToken = default)
        {
            var entity = await GetByIdAsync(id, ownerId, cancellationToken);
            if (entity is null)
            {
                _logger.LogWarning("Collection {CollectionId} not found for owner {OwnerId}", id, ownerId);
                return false;
            }
            _db.Collections.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> ExistsAsync(Guid ownerId, string name, CancellationToken cancellationToken = default)
        {
            return await _db.Collections.AnyAsync(c => c.OwnerId == ownerId && c.Name == name, cancellationToken);
        }
    }
}