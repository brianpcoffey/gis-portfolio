using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories.Mappings;

namespace Portfolio.Repositories
{
    public class PortfolioDbContext : DbContext
    {
        public PortfolioDbContext(DbContextOptions<PortfolioDbContext> options) : base(options) { }

        public DbSet<UserNote> UserNotes { get; set; }
        public DbSet<SavedFeature> SavedFeatures { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new SavedFeatureMap());
            modelBuilder.ApplyConfiguration(new UserNoteMap());
        }
    }
}
