using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories;

public class FiberShipmentRepository : IFiberShipmentRepository
{
    private readonly PortfolioDbContext _db;
    public FiberShipmentRepository(PortfolioDbContext db) => _db = db;

    public async Task<List<FiberShipment>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.FiberShipments
            .AsNoTracking()
            .Include(s => s.Order).ThenInclude(o => o.Client)
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<FiberShipment?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.FiberShipments
            .AsNoTracking()
            .Include(s => s.Order).ThenInclude(o => o.Client)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken);
    }

    public async Task<FiberShipment> AddAsync(FiberShipment shipment, CancellationToken cancellationToken = default)
    {
        _db.FiberShipments.Add(shipment);
        await _db.SaveChangesAsync(cancellationToken);
        await _db.Entry(shipment).Reference(s => s.Order).LoadAsync(cancellationToken);
        return shipment;
    }

    public async Task<FiberShipment> UpdateStatusAsync(int id, string status, Guid userId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.FiberShipments.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken);
        if (shipment is null) throw new KeyNotFoundException($"Shipment {id} not found.");
        shipment.Status = status;
        await _db.SaveChangesAsync(cancellationToken);
        await _db.Entry(shipment).Reference(s => s.Order).LoadAsync(cancellationToken);
        return shipment;
    }
}
