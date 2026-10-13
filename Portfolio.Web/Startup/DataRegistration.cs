using Microsoft.EntityFrameworkCore;
using Portfolio.Repositories;
using Portfolio.Repositories.Interfaces;
using Portfolio.Repositories.Repositories;
using Portfolio.Services;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;

namespace Portfolio.Web.Startup;

internal static class DataRegistration
{
    internal static IServiceCollection AddPortfolioRepositories(this IServiceCollection services)
    {
        services.AddScoped<IFiberOrderRepository, FiberOrderRepository>();
        services.AddScoped<IFiberShipmentRepository, FiberShipmentRepository>();
        services.AddScoped<IFiberMaterialRepository, FiberMaterialRepository>();
        services.AddScoped<IFiberInventoryTransactionRepository, FiberInventoryTransactionRepository>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();
        services.AddScoped<IPropertyRepository, PropertyRepository>();
        services.AddScoped<ISavedFeatureRepository, SavedFeatureRepository>();
        services.AddScoped<ISavedSearchRepository, SavedSearchRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        return services;
    }

    internal static IServiceCollection AddPortfolioServices(this IServiceCollection services)
    {
        services.AddScoped<IFiberOrderService, FiberOrderService>();
        services.AddScoped<IFiberShipmentService, FiberShipmentService>();
        services.AddScoped<IFiberMaterialService, FiberMaterialService>();
        services.AddScoped<IFiberDashboardService, FiberDashboardService>();
        services.AddScoped<ICollectionService, CollectionService>();
        services.AddScoped<IHomeScoringService, HomeScoringService>();
        services.AddScoped<IGeoStreamProcessorService, GeoStreamProcessorService>();
        services.AddScoped<ISpatialGeometryService, SpatialGeometryService>();
        services.AddScoped<IRasterTerrainService, RasterTerrainService>();
        services.AddScoped<ISpatialGraphService, SpatialGraphService>();
        services.AddScoped<ISpatialClusterService, SpatialClusterService>();
        services.AddScoped<IViewshedService, ViewshedService>();
        services.AddScoped<ISpatialOverlayService, SpatialOverlayService>();
        services.AddScoped<ICatRiskService, CatRiskService>();
        services.AddScoped<IChangeDetectionService, ChangeDetectionService>();
        services.AddScoped<IOutageTraceService, OutageTraceService>();
        services.AddScoped<IResponseCoverageService, ResponseCoverageService>();
        services.AddScoped<IFleetRoutingService, FleetRoutingService>();
        services.AddScoped<ISavedFeatureService, SavedFeatureService>();
        services.AddScoped<UserProfileSeedService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<ISavedSearchService, SavedSearchService>();
        return services;
    }

    internal static IServiceCollection AddPortfolioDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseUrl = Portfolio.Web.Infrastructure.ConnectionStrings.ResolveDatabaseUrl(
            configuration, Environment.GetEnvironmentVariable);

        var npgsqlBuilder = Portfolio.Web.Infrastructure.ConnectionStrings.ParsePostgres(databaseUrl);

        // Use the parsed/normalized connection string (handles postgres:// URI format)
        services.AddDbContext<PortfolioDbContext>(options =>
            options.UseNpgsql(npgsqlBuilder.ConnectionString));
        return services;
    }
}
