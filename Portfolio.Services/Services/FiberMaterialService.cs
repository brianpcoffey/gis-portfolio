using Microsoft.EntityFrameworkCore;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services;

public class FiberMaterialService : IFiberMaterialService
{
    private readonly IFiberMaterialRepository _materialRepo;
    private readonly IFiberInventoryTransactionRepository _transactionRepo;
    private readonly IUserProfileService _userProfileService;
    private readonly TimeProvider _timeProvider;
    private readonly PortfolioDbContext _db;

    public FiberMaterialService(
        IFiberMaterialRepository materialRepo,
        IFiberInventoryTransactionRepository transactionRepo,
        IUserProfileService userProfileService,
        TimeProvider timeProvider,
        PortfolioDbContext db)
    {
        _materialRepo = materialRepo;
        _transactionRepo = transactionRepo;
        _userProfileService = userProfileService;
        _timeProvider = timeProvider;
        _db = db;
    }

    private Guid CurrentUserId =>
        _userProfileService.GetCurrentUserId() ?? throw new InvalidOperationException("User not identified");

    public async Task<List<FiberMaterialDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var materials = await _materialRepo.GetAllAsync(CurrentUserId, cancellationToken);
        return materials.Select(MapToDto).ToList();
    }

    public async Task<FiberMaterialDto> ReceiveStockAsync(int id, ReceiveStockDto dto, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var material = await _materialRepo.GetByIdAsync(id, userId, cancellationToken)
                ?? throw new KeyNotFoundException($"Material {id} not found");
            var before = material.QtyOnHand;
            material.QtyOnHand += dto.Quantity;
            material.LastUpdated = _timeProvider.GetUtcNow().UtcDateTime;
            await _materialRepo.UpdateAsync(material, cancellationToken);
            var inv = new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = id,
                TransactionType = "Receive",
                Quantity = dto.Quantity,
                QtyBeforeTransaction = before,
                QtyAfterTransaction = material.QtyOnHand,
                Notes = dto.Notes,
                TransactionDate = _timeProvider.GetUtcNow().UtcDateTime
            };
            await _transactionRepo.AddAsync(inv, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return MapToDto(material);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static FiberMaterialDto MapToDto(FiberMaterial m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Sku = m.Sku,
        QtyOnHand = m.QtyOnHand,
        UnitCost = m.UnitCost,
        TotalValue = m.QtyOnHand * m.UnitCost,
        IsLowStock = m.QtyOnHand <= m.ReorderPoint,
        ReorderPoint = m.ReorderPoint
    };
}
