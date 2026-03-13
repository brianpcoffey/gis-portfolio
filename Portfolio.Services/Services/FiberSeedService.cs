using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services;

public class FiberSeedService : IFiberSeedService
{
    private readonly IFiberClientRepository _clientRepo;
    private readonly IFiberOrderRepository _orderRepo;
    private readonly IFiberShipmentRepository _shipmentRepo;
    private readonly IFiberMaterialRepository _materialRepo;
    private readonly IFiberInventoryTransactionRepository _transactionRepo;
    private readonly IUserProfileService _userProfileService;
    private readonly TimeProvider _timeProvider;
    private readonly PortfolioDbContext _db;

    public FiberSeedService(
        IFiberClientRepository clientRepo,
        IFiberOrderRepository orderRepo,
        IFiberShipmentRepository shipmentRepo,
        IFiberMaterialRepository materialRepo,
        IFiberInventoryTransactionRepository transactionRepo,
        IUserProfileService userProfileService,
        TimeProvider timeProvider,
        PortfolioDbContext db)
    {
        _clientRepo = clientRepo;
        _orderRepo = orderRepo;
        _shipmentRepo = shipmentRepo;
        _materialRepo = materialRepo;
        _transactionRepo = transactionRepo;
        _userProfileService = userProfileService;
        _timeProvider = timeProvider;
        _db = db;
    }

    private Guid CurrentUserId =>
        _userProfileService.GetCurrentUserId() ?? throw new InvalidOperationException("User not identified");

    public async Task<bool> UserHasSeedDataAsync(CancellationToken cancellationToken = default)
    {
        return await _db.FiberClients.AnyAsync(c => c.UserId == CurrentUserId, cancellationToken);
    }

    public async Task SeedForUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Seed clients
            var clients = new List<FiberClient>
            {
                new() { UserId = userId, Name = "Gulf Coast Chemical", ContactName = "", Email = "", Phone = "", City = "Houston", State = "TX", Latitude = 29.7604, Longitude = -95.3698, CreatedDate = _timeProvider.GetUtcNow().UtcDateTime },
                new() { UserId = userId, Name = "Lone Star Refining", ContactName = "", Email = "", Phone = "", City = "Beaumont", State = "TX", Latitude = 30.0860, Longitude = -94.1018, CreatedDate = _timeProvider.GetUtcNow().UtcDateTime },
                new() { UserId = userId, Name = "Delta Processing Co", ContactName = "", Email = "", Phone = "", City = "Baton Rouge", State = "LA", Latitude = 30.4515, Longitude = -91.1871, CreatedDate = _timeProvider.GetUtcNow().UtcDateTime },
                new() { UserId = userId, Name = "Bayou Industrial", ContactName = "", Email = "", Phone = "", City = "New Orleans", State = "LA", Latitude = 29.9511, Longitude = -90.0715, CreatedDate = _timeProvider.GetUtcNow().UtcDateTime },
                new() { UserId = userId, Name = "Sooner Plant Services", ContactName = "", Email = "", Phone = "", City = "Oklahoma City", State = "OK", Latitude = 35.4676, Longitude = -97.5164, CreatedDate = _timeProvider.GetUtcNow().UtcDateTime },
                new() { UserId = userId, Name = "Arkansas Fabricators", ContactName = "", Email = "", Phone = "", City = "Little Rock", State = "AR", Latitude = 34.7465, Longitude = -92.2896, CreatedDate = _timeProvider.GetUtcNow().UtcDateTime },
                new() { UserId = userId, Name = "Magnolia Chemical", ContactName = "", Email = "", Phone = "", City = "Jackson", State = "MS", Latitude = 32.2988, Longitude = -90.1848, CreatedDate = _timeProvider.GetUtcNow().UtcDateTime },
                new() { UserId = userId, Name = "Steel City Industries", ContactName = "", Email = "", Phone = "", City = "Birmingham", State = "AL", Latitude = 33.5186, Longitude = -86.8104, CreatedDate = _timeProvider.GetUtcNow().UtcDateTime },
                new() { UserId = userId, Name = "Cumberland Manufacturing", ContactName = "", Email = "", Phone = "", City = "Nashville", State = "TN", Latitude = 36.1627, Longitude = -86.7816, CreatedDate = _timeProvider.GetUtcNow().UtcDateTime },
                new() { UserId = userId, Name = "Peach State Processing", ContactName = "", Email = "", Phone = "", City = "Atlanta", State = "GA", Latitude = 33.7490, Longitude = -84.3880, CreatedDate = _timeProvider.GetUtcNow().UtcDateTime }
            };
            _db.FiberClients.AddRange(clients);
            await _db.SaveChangesAsync(cancellationToken);
            // Orders, Shipments, Materials, Transactions seeding omitted for brevity
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
