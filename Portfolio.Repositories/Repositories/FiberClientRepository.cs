using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories;

public class FiberClientRepository : IFiberClientRepository
{
    private readonly PortfolioDbContext _db;
    public FiberClientRepository(PortfolioDbContext db) => _db = db;

    public async Task<List<FiberClient>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.FiberClients
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<FiberClient?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.FiberClients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);
    }

    public async Task<FiberClient> AddAsync(FiberClient client, CancellationToken cancellationToken = default)
    {
        _db.FiberClients.Add(client);
        await _db.SaveChangesAsync(cancellationToken);
        return client;
    }

    public async Task<FiberClient> UpdateAsync(FiberClient client, CancellationToken cancellationToken = default)
    {
        _db.FiberClients.Update(client);
        await _db.SaveChangesAsync(cancellationToken);
        return client;
    }

    public async Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.FiberClients.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);
        if (entity is null) return false;
        _db.FiberClients.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
