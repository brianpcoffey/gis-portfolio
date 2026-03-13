using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories;

public class FiberMaterialRepository : IFiberMaterialRepository
{
    private readonly PortfolioDbContext _db;
    public FiberMaterialRepository(PortfolioDbContext db) => _db = db;

    public async Task<List<FiberMaterial>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.FiberMaterials
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<FiberMaterial?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.FiberMaterials
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, cancellationToken);
    }

    public async Task<FiberMaterial> AddAsync(FiberMaterial material, CancellationToken cancellationToken = default)
    {
        _db.FiberMaterials.Add(material);
        await _db.SaveChangesAsync(cancellationToken);
        return material;
    }

    public async Task<FiberMaterial> UpdateAsync(FiberMaterial material, CancellationToken cancellationToken = default)
    {
        _db.FiberMaterials.Update(material);
        await _db.SaveChangesAsync(cancellationToken);
        return material;
    }

    public async Task<List<FiberMaterial>> GetLowStockAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.FiberMaterials
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.QtyOnHand < m.ReorderPoint)
            .ToListAsync(cancellationToken);
    }
}
