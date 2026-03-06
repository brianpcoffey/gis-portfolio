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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new SavedFeatureMap());
            modelBuilder.ApplyConfiguration(new CollectionMap());
            modelBuilder.Entity<UserProfile>(b =>
            {
                b.HasKey(x => x.UserId);
                b.Property(x => x.CreatedDate).IsRequired();
                b.Property(x => x.LastActiveDate).IsRequired();
                b.HasMany(x => x.Claims).WithOne(c => c.UserProfile).HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserClaim>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.ClaimType).IsRequired().HasMaxLength(200);
                b.Property(x => x.ClaimValue).IsRequired();
                b.HasIndex(x => new { x.UserId, x.ClaimType }).IsUnique(false);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
