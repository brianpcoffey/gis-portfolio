using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Portfolio.Repositories
{
    /// <summary>
    /// Lets EF Core design-time tooling (e.g. <c>dotnet ef migrations add</c>) construct the
    /// context without starting the web host. The connection string is a placeholder — migration
    /// scaffolding only needs the model, not a live database — so this never connects anywhere.
    /// Run migrations with:
    ///   dotnet ef migrations add &lt;Name&gt; --project Portfolio.Repositories --startup-project Portfolio.Repositories
    /// </summary>
    public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PortfolioDbContext>
    {
        public PortfolioDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<PortfolioDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=design_time;Username=postgres;Password=postgres")
                .Options;
            return new PortfolioDbContext(options);
        }
    }
}
