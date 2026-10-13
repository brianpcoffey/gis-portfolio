using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Portfolio.Common.Models;
using Portfolio.Repositories;
using Portfolio.Repositories.Diagnostics;
using Portfolio.Services.Data;

namespace Portfolio.Services.Services;

public class UserProfileSeedService
{
    private readonly PortfolioDbContext _db;
    private readonly ILogger<UserProfileSeedService> _logger;
    private const string InMemoryProviderName = "Microsoft.EntityFrameworkCore.InMemory";

    public UserProfileSeedService(PortfolioDbContext db, ILogger<UserProfileSeedService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var providerName = _db.Database.ProviderName;
        IDbContextTransaction? tx = null;
        try
        {
            // The EF InMemory provider (used by the tests) does not support transactions, so seed without one there.
            if (string.IsNullOrEmpty(providerName) ||
                !string.Equals(providerName, InMemoryProviderName, StringComparison.OrdinalIgnoreCase))
            {
                tx = await _db.Database.BeginTransactionAsync();
            }

            await SeedClientsAsync(userId, cancellationToken);
            await SeedMaterialsAsync(userId, cancellationToken);

            var clients = await FindClientsAsync(userId, cancellationToken);
            await SeedOrdersAsync(userId, clients, cancellationToken);
            await SeedShipmentsAsync(userId, clients, cancellationToken);

            await SeedInventoryTransactionsAsync(userId, cancellationToken);

            if (tx != null)
            {
                await tx.CommitAsync();
            }
            _logger.LogDebug("Seed complete for user {UserId}", LogRedaction.UserId(userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding data for user {UserId}", LogRedaction.UserId(userId));
            if (tx != null)
            {
                await tx.RollbackAsync();
            }
            throw;
        }
        finally
        {
            if (tx != null)
            {
                await tx.DisposeAsync();
            }
        }
    }

    private async Task SeedClientsAsync(Guid userId, CancellationToken cancellationToken)
    {
        foreach (var client in FiberDemoSeedData.Clients(userId))
        {
            var name = client.Name;
            if (!await _db.FiberClients.AnyAsync(c => c.UserId == userId && c.Name == name, cancellationToken))
            {
                _db.FiberClients.Add(client);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedMaterialsAsync(Guid userId, CancellationToken cancellationToken)
    {
        foreach (var material in FiberDemoSeedData.Materials(userId))
        {
            var sku = material.Sku;
            if (!await _db.FiberMaterials.AnyAsync(m => m.UserId == userId && m.Sku == sku, cancellationToken))
            {
                _db.FiberMaterials.Add(material);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // One lookup per demo client; a missing client (null) makes the orders and shipments
    // that depend on it skip.
    private async Task<Dictionary<string, FiberClient?>> FindClientsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var clients = new Dictionary<string, FiberClient?>();
        foreach (var seed in FiberDemoSeedData.Clients(userId))
        {
            var name = seed.Name;
            clients[name] = await _db.FiberClients
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == name, cancellationToken);
        }

        return clients;
    }

    private async Task SeedOrdersAsync(Guid userId, Dictionary<string, FiberClient?> clients, CancellationToken cancellationToken)
    {
        foreach (var order in FiberDemoSeedData.Orders(userId))
        {
            var client = clients[order.ClientName];
            var productName = order.ProductName;
            var orderNumber = order.OrderNumber;
            // A user may have edited a demo order's client or product (PUT allows it); the number is still theirs,
            // and the unique (user, order number) index would reject a second copy, so skip on either match.
            if (client != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && (o.OrderNumber == orderNumber || (o.ClientName == client.Name && o.ProductName == productName)), cancellationToken))
            {
                _db.FiberOrders.Add(order);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedShipmentsAsync(Guid userId, Dictionary<string, FiberClient?> clients, CancellationToken cancellationToken)
    {
        foreach (var (clientName, shipment) in FiberDemoSeedData.Shipments(userId))
        {
            var trackingNumber = shipment.TrackingNumber;
            if (clients[clientName] != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == trackingNumber, cancellationToken))
            {
                _db.FiberShipments.Add(shipment);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedInventoryTransactionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var transactions = FiberDemoSeedData.InventoryTransactions(userId);

        var materials = new Dictionary<string, FiberMaterial?>();
        foreach (var sku in transactions.Select(t => t.Sku).Distinct())
        {
            materials[sku] = await _db.FiberMaterials
                .FirstOrDefaultAsync(m => m.UserId == userId && m.Sku == sku, cancellationToken);
        }

        foreach (var (sku, transaction) in transactions)
        {
            var material = materials[sku];
            var notes = transaction.Notes;
            if (material != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == notes, cancellationToken))
            {
                transaction.MaterialId = material.Id;
                _db.FiberInventoryTransactions.Add(transaction);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
