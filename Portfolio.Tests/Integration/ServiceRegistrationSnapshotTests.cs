using Portfolio.Tests.Helpers;

namespace Portfolio.Tests.Integration;

/// <summary>
/// Golden list of the DI registrations Program.cs makes for application types: sorted
/// "ServiceType | Lifetime | ImplementationType" for Portfolio.* plus IBatchJobStore,
/// IDistributedCache, TimeProvider and IMemoryCache. "(factory)" is a registration made through a
/// factory delegate (the typed HttpClient registrations). The duplicate geocoding entries are
/// today's behaviour: each AddScoped is followed by an AddHttpClient of the same interface, and the
/// later (transient, typed-client) descriptor is the one that wins.
/// </summary>
public class ServiceRegistrationSnapshotTests
{
    private const string Expected = """
        Microsoft.Extensions.Caching.Distributed.IDistributedCache | Singleton | Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache
        Microsoft.Extensions.Caching.Memory.IMemoryCache | Singleton | Microsoft.Extensions.Caching.Memory.MemoryCache
        Portfolio.Repositories.Interfaces.ICollectionRepository | Scoped | Portfolio.Repositories.Repositories.CollectionRepository
        Portfolio.Repositories.Interfaces.IFiberClientRepository | Scoped | Portfolio.Repositories.Repositories.FiberClientRepository
        Portfolio.Repositories.Interfaces.IFiberInventoryTransactionRepository | Scoped | Portfolio.Repositories.Repositories.FiberInventoryTransactionRepository
        Portfolio.Repositories.Interfaces.IFiberMaterialRepository | Scoped | Portfolio.Repositories.Repositories.FiberMaterialRepository
        Portfolio.Repositories.Interfaces.IFiberOrderRepository | Scoped | Portfolio.Repositories.Repositories.FiberOrderRepository
        Portfolio.Repositories.Interfaces.IFiberShipmentRepository | Scoped | Portfolio.Repositories.Repositories.FiberShipmentRepository
        Portfolio.Repositories.Interfaces.IPropertyRepository | Scoped | Portfolio.Repositories.Repositories.PropertyRepository
        Portfolio.Repositories.Interfaces.ISavedFeatureRepository | Scoped | Portfolio.Repositories.Repositories.SavedFeatureRepository
        Portfolio.Repositories.Interfaces.ISavedSearchRepository | Scoped | Portfolio.Repositories.Repositories.SavedSearchRepository
        Portfolio.Repositories.Interfaces.IUserProfileRepository | Scoped | Portfolio.Repositories.Repositories.UserProfileRepository
        Portfolio.Repositories.PortfolioDbContext | Scoped | Portfolio.Repositories.PortfolioDbContext
        Portfolio.Services.Abstractions.IBatchJobStore | Singleton | Portfolio.Services.InMemoryBatchJobStore
        Portfolio.Services.Interfaces.IAddressStandardizationService | Scoped | Portfolio.Services.Services.AddressStandardizationService
        Portfolio.Services.Interfaces.IAddressStandardizationService | Transient | (factory)
        Portfolio.Services.Interfaces.IArcGisService | Transient | (factory)
        Portfolio.Services.Interfaces.IBatchGeocodingService | Scoped | Portfolio.Services.Services.BatchGeocodingService
        Portfolio.Services.Interfaces.IBatchGeocodingService | Transient | (factory)
        Portfolio.Services.Interfaces.ICatRiskService | Scoped | Portfolio.Services.Services.CatRiskService
        Portfolio.Services.Interfaces.IChangeDetectionService | Scoped | Portfolio.Services.Services.ChangeDetectionService
        Portfolio.Services.Interfaces.ICollectionService | Scoped | Portfolio.Services.Services.CollectionService
        Portfolio.Services.Interfaces.IFiberDashboardService | Scoped | Portfolio.Services.Services.FiberDashboardService
        Portfolio.Services.Interfaces.IFiberMaterialService | Scoped | Portfolio.Services.Services.FiberMaterialService
        Portfolio.Services.Interfaces.IFiberOrderService | Scoped | Portfolio.Services.Services.FiberOrderService
        Portfolio.Services.Interfaces.IFiberShipmentService | Scoped | Portfolio.Services.Services.FiberShipmentService
        Portfolio.Services.Interfaces.IFleetRoutingService | Scoped | Portfolio.Services.Services.FleetRoutingService
        Portfolio.Services.Interfaces.IGeoStreamProcessorService | Scoped | Portfolio.Services.Services.GeoStreamProcessorService
        Portfolio.Services.Interfaces.IHomeScoringService | Scoped | Portfolio.Services.Services.HomeScoringService
        Portfolio.Services.Interfaces.IOutageTraceService | Scoped | Portfolio.Services.Services.OutageTraceService
        Portfolio.Services.Interfaces.IRasterTerrainService | Scoped | Portfolio.Services.Services.RasterTerrainService
        Portfolio.Services.Interfaces.IResponseCoverageService | Scoped | Portfolio.Services.Services.ResponseCoverageService
        Portfolio.Services.Interfaces.IReverseGeocodingService | Scoped | Portfolio.Services.Services.ReverseGeocodingService
        Portfolio.Services.Interfaces.IReverseGeocodingService | Transient | (factory)
        Portfolio.Services.Interfaces.ISavedFeatureService | Scoped | Portfolio.Services.Services.SavedFeatureService
        Portfolio.Services.Interfaces.ISavedSearchService | Scoped | Portfolio.Services.Services.SavedSearchService
        Portfolio.Services.Interfaces.ISpatialClusterService | Scoped | Portfolio.Services.Services.SpatialClusterService
        Portfolio.Services.Interfaces.ISpatialGeometryService | Scoped | Portfolio.Services.Services.SpatialGeometryService
        Portfolio.Services.Interfaces.ISpatialGraphService | Scoped | Portfolio.Services.Services.SpatialGraphService
        Portfolio.Services.Interfaces.ISpatialOverlayService | Scoped | Portfolio.Services.Services.SpatialOverlayService
        Portfolio.Services.Interfaces.IUserProfileService | Scoped | Portfolio.Services.Services.UserProfileService
        Portfolio.Services.Interfaces.IViewshedService | Scoped | Portfolio.Services.Services.ViewshedService
        Portfolio.Services.Services.UserProfileSeedService | Scoped | Portfolio.Services.Services.UserProfileSeedService
        System.TimeProvider | Singleton | System.TimeProvider+SystemTimeProvider
        """;

    [Fact]
    public async Task ApplicationServiceRegistrations_MatchTheGoldenList()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        _ = f.Client(); // build the host so ConfigureTestServices captures the descriptors

        // Act
        var actual = f.Registrations
            .Where(r => r.Service.FullName!.StartsWith("Portfolio.", StringComparison.Ordinal) ||
                        r.Service.Name is "IBatchJobStore" or "IDistributedCache" or "TimeProvider" or "IMemoryCache")
            .Select(r => $"{r.Service.FullName} | {r.Lifetime} | {r.Impl}")
            .Order(StringComparer.Ordinal)
            .ToList();

        var expected = Expected.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        // Assert
        GoldenAssert.Equal(expected, actual);
    }
}
