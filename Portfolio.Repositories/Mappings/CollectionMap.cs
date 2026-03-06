using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Common.Models;

namespace Portfolio.Repositories.Mappings
{
    public class CollectionMap : IEntityTypeConfiguration<Collection>
    {
        public void Configure(EntityTypeBuilder<Collection> builder)
        {
            builder.ToTable("Collections");

            builder.HasKey(c => c.Id);

            builder.Property(c => c.OwnerId)
                .IsRequired();

            builder.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(c => c.Color)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(c => c.CreatedAt)
                .IsRequired();

            builder.Property(c => c.LastModified);

            builder.HasIndex(c => new { c.OwnerId, c.Name }).IsUnique();
        }
    }
}