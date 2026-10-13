using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories;

public class FiberInventoryTransactionRepository : IFiberInventoryTransactionRepository
{
    private readonly PortfolioDbContext _db;
    public FiberInventoryTransactionRepository(PortfolioDbContext db) => _db = db;

    public async Task<FiberInventoryTransaction> AddAsync(FiberInventoryTransaction transaction, CancellationToken cancellationToken = default)
    {
        _db.FiberInventoryTransactions.Add(transaction);
        await _db.SaveChangesAsync(cancellationToken);
        await _db.Entry(transaction).Reference(t => t.Material).LoadAsync(cancellationToken);
        return transaction;
    }

}
