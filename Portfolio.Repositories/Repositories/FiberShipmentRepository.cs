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
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<FiberShipment?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.FiberShipments
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken);
    }
    public async Task<FiberShipment> UpdateAsync(int id, Portfolio.Common.DTOs.FiberShipmentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.FiberShipments.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken);
        if (shipment is null) throw new KeyNotFoundException($"Shipment {id} not found.");
        shipment.TrackingNumber = dto.TrackingNumber;
        shipment.CarrierName = dto.CarrierName;
        shipment.Status = dto.Status;
        shipment.DestinationCity = dto.DestinationCity;
        shipment.DestinationState = dto.DestinationState;
        shipment.OriginLat = dto.OriginLat;
        shipment.OriginLng = dto.OriginLng;
        shipment.DestinationLat = dto.DestinationLat;
        shipment.DestinationLng = dto.DestinationLng;
        shipment.EstimatedArrival = dto.EstimatedArrival;
        shipment.RouteJson = dto.Route != null && dto.Route.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(dto.Route) : null;
        await _db.SaveChangesAsync(cancellationToken);
        return shipment;
    }

    public async Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.FiberShipments.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken);
        if (shipment is null) return false;
        _db.FiberShipments.Remove(shipment);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<FiberShipment> AddAsync(FiberShipment shipment, CancellationToken cancellationToken = default)
    {
        _db.FiberShipments.Add(shipment);
        await _db.SaveChangesAsync(cancellationToken);
        return shipment;
    }

    public async Task<FiberShipment> UpdateStatusAsync(int id, string status, Guid userId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.FiberShipments.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken);
        if (shipment is null) throw new KeyNotFoundException($"Shipment {id} not found.");
        shipment.Status = status;
        await _db.SaveChangesAsync(cancellationToken);
        return shipment;
    }
}
