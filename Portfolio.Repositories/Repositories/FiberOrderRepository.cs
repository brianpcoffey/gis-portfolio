using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories;

public class FiberOrderRepository : IFiberOrderRepository
{
    private readonly PortfolioDbContext _db;
    public FiberOrderRepository(PortfolioDbContext db) => _db = db;

    public async Task<List<FiberOrder>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.FiberOrders
            .AsNoTracking()
            .Include(o => o.Client)
            .Where(o => o.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<FiberOrder?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.FiberOrders
            .AsNoTracking()
            .Include(o => o.Client)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId, cancellationToken);
    }

    public async Task<List<FiberOrder>> GetByStatusAsync(string status, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.FiberOrders
            .AsNoTracking()
            .Include(o => o.Client)
            .Where(o => o.Status == status && o.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FiberOrder>> GetByDateRangeAsync(DateTime start, DateTime end, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.FiberOrders
            .AsNoTracking()
            .Include(o => o.Client)
            .Where(o => o.OrderDate >= start && o.OrderDate <= end && o.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<FiberOrder> AddAsync(FiberOrder order, CancellationToken cancellationToken = default)
    {
        _db.FiberOrders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);
        await _db.Entry(order).Reference(o => o.Client).LoadAsync(cancellationToken);
        return order;
    }

    public async Task<FiberOrder> UpdateAsync(FiberOrder order, CancellationToken cancellationToken = default)
    {
        _db.FiberOrders.Update(order);
        await _db.SaveChangesAsync(cancellationToken);
        await _db.Entry(order).Reference(o => o.Client).LoadAsync(cancellationToken);
        return order;
    }

    public async Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.FiberOrders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId, cancellationToken);
        if (entity is null) return false;
        _db.FiberOrders.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
