using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Common.Models;

namespace Portfolio.Repositories.Mappings
{
    public class PropertyMap : IEntityTypeConfiguration<Property>
    {
        public void Configure(EntityTypeBuilder<Property> builder)
        {
            builder.ToTable("Properties");

            builder.HasKey(p => p.Id);

            // Listing basics
            builder.Property(p => p.BrokeredBy).HasMaxLength(200);
            builder.Property(p => p.Status).IsRequired().HasMaxLength(50).HasDefaultValue("active");
            builder.Property(p => p.Price).IsRequired().HasColumnType("numeric(12,2)");
            builder.Property(p => p.AcreLot).HasColumnType("numeric(8,2)");

            // Address
            builder.Property(p => p.Street).IsRequired().HasMaxLength(300);
            builder.Property(p => p.City).IsRequired().HasMaxLength(100).HasDefaultValue("Redlands");
            builder.Property(p => p.State).IsRequired().HasMaxLength(2).HasDefaultValue("CA");
            builder.Property(p => p.ZipCode).IsRequired().HasMaxLength(10);

            // GIS coordinates
            builder.Property(p => p.Latitude).IsRequired();
            builder.Property(p => p.Longitude).IsRequired();

            // Financial
            builder.Property(p => p.HoaFee).HasColumnType("numeric(10,2)");
            builder.Property(p => p.PropertyTax).HasColumnType("numeric(10,2)");
            builder.Property(p => p.Utilities).HasColumnType("numeric(10,2)");

            // Indexes
            builder.HasIndex(p => new { p.City, p.ZipCode });
            builder.HasIndex(p => p.Price);
        }
    }
}