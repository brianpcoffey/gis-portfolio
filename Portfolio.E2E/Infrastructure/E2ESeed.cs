using Microsoft.Extensions.DependencyInjection;
using Portfolio.Repositories;
using Portfolio.Repositories.Seed;

namespace Portfolio.E2E.Infrastructure;

/// <summary>Reference data that production gets from migrations and an EF InMemory host does not have.</summary>
internal static class E2ESeed
{
    /// <summary>
    /// Inserts the Redlands properties (production inserts them in the <c>ExpandRedlandsProperties</c>
    /// migration), so Home Finder has homes to rank. Everything else is created by the real code paths
    /// (visitor data at sign-in).
    /// </summary>
    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();

        db.Properties.AddRange(RedlandsPropertySeedData.All);
        await db.SaveChangesAsync();
    }
}
