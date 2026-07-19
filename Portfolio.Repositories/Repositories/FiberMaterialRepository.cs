using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories;

public class FiberMaterialRepository : IFiberMaterialRepository
{
    private readonly PortfolioDbContext _db;
    private readonly TimeProvider _timeProvider;

    public FiberMaterialRepository(PortfolioDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

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

    public async Task<List<FiberMaterial>> GetLowStockAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.FiberMaterials
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.QtyOnHand < m.ReorderPoint)
            .ToListAsync(cancellationToken);
    }

    public async Task<FiberMaterial> UpdateAsync(int id, Portfolio.Common.DTOs.FiberMaterialDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var material = await _db.FiberMaterials.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, cancellationToken);
        if (material is null) throw new KeyNotFoundException($"Material {id} not found.");
        material.Name = dto.Name;
        material.Sku = dto.Sku;
        material.Category = dto.Category;
        material.QtyOnHand = dto.QtyOnHand;
        material.UnitCost = dto.UnitCost;
        material.ReorderPoint = dto.ReorderPoint;
        material.LastUpdated = _timeProvider.GetUtcNow().UtcDateTime; // stamp on every edit
        await _db.SaveChangesAsync(cancellationToken);
        return material;
    }

    public async Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        var material = await _db.FiberMaterials.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, cancellationToken);
        if (material is null) return false;
        _db.FiberMaterials.Remove(material);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
