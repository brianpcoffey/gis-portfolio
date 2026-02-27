using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Common.Models;

namespace Portfolio.Repositories.Mappings
{
    public class SavedFeatureMap : IEntityTypeConfiguration<SavedFeature>
    {
        public void Configure(EntityTypeBuilder<SavedFeature> builder)
        {
            builder.ToTable("SavedFeatures");
            builder.HasKey(f => f.Id);

            builder.Property(f => f.UserId).IsRequired();
            builder.Property(f => f.LayerId).IsRequired();
            builder.Property(f => f.FeatureId).IsRequired();
            builder.Property(f => f.Name).IsRequired().HasMaxLength(200);
            builder.Property(f => f.GeometryJson).IsRequired();
            builder.Property(f => f.Description);

            builder.Property(f => f.CollectionId);
            builder.HasOne(f => f.Collection)
                   .WithMany(c => c.SavedFeatures)
                   .HasForeignKey(f => f.CollectionId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.Property(f => f.DateSaved).IsRequired();
            builder.Property(f => f.LastModified);

            builder.HasIndex(f => new { f.UserId, f.LayerId, f.FeatureId }).IsUnique();
        }
    }
}