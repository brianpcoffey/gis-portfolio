using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories.Mappings;

namespace Portfolio.Repositories
{
    public class PortfolioDbContext : DbContext
    {
        public PortfolioDbContext(DbContextOptions<PortfolioDbContext> options) : base(options) { }

        public DbSet<SavedFeature> SavedFeatures { get; set; }
        public DbSet<Collection> Collections { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; } = null!;
        public DbSet<UserClaim> UserClaims { get; set; } = null!;
        public DbSet<Property> Properties { get; set; } = null!;
        public DbSet<SavedSearch> SavedSearches { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new SavedFeatureMap());
            modelBuilder.ApplyConfiguration(new CollectionMap());
            modelBuilder.ApplyConfiguration(new PropertyMap());
            modelBuilder.ApplyConfiguration(new SavedSearchMap());
            modelBuilder.ApplyConfiguration(new UserProfileMap());
            modelBuilder.ApplyConfiguration(new UserClaimMap());

            base.OnModelCreating(modelBuilder);
        }
    }
}
