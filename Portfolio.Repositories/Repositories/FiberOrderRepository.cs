using Microsoft.EntityFrameworkCore;
using Npgsql;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Repositories.Mappings;

namespace Portfolio.Repositories.Repositories;

public class FiberOrderRepository : IFiberOrderRepository
{
    private readonly PortfolioDbContext _db;
    public FiberOrderRepository(PortfolioDbContext db) => _db = db;

    public async Task<List<FiberOrder>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.FiberOrders
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<FiberOrder?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.FiberOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId, cancellationToken);
    }

    public async Task<List<string>> GetOrderNumbersAsync(Guid userId, string prefix, CancellationToken cancellationToken = default)
    {
        return await _db.FiberOrders
            .AsNoTracking()
            .Where(o => o.UserId == userId && o.OrderNumber.StartsWith(prefix))
            .Select(o => o.OrderNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<FiberOrder?> TryAddAsync(FiberOrder order, CancellationToken cancellationToken = default)
    {
        var entry = _db.FiberOrders.Add(order);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return order;
        }
        catch (DbUpdateException ex) when (IsOrderNumberClash(ex))
        {
            // Another create by the same user took this number first. Stop tracking the rejected row so a retry
            // starts clean.
            entry.State = EntityState.Detached;
            return null;
        }
    }

    // Only the (user, order number) unique index counts as a clash; any other failure is a real error.
    private static bool IsOrderNumberClash(DbUpdateException ex) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: FiberOrderMapping.OrderNumberIndexName
        };

    public async Task<FiberOrder> UpdateAsync(FiberOrder order, CancellationToken cancellationToken = default)
    {
        _db.FiberOrders.Update(order);
        await _db.SaveChangesAsync(cancellationToken);
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
