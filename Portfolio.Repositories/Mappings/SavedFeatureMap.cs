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

            builder.Property(f => f.LayerId).IsRequired().HasMaxLength(100);
            builder.Property(f => f.FeatureId).IsRequired().HasMaxLength(100);
            builder.Property(f => f.Name).IsRequired().HasMaxLength(200);
            builder.Property(f => f.GeometryJson).IsRequired();
            builder.Property(f => f.DateSaved).IsRequired();

            builder.HasMany(f => f.UserNotes)
                   .WithOne(n => n.SavedFeature)
                   .HasForeignKey(n => n.SavedFeatureId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}