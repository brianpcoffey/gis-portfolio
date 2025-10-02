using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;

namespace Portfolio.Repositories
{
    public class GisDbContext : DbContext
    {
        public GisDbContext(DbContextOptions<GisDbContext> options) : base(options) { }
        public DbSet<GisFeature> GisFeatures { get; set; }
        // Expand with User/Role tables for production
    }
}
