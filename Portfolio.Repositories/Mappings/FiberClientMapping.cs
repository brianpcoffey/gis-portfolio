using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Common.Models;

namespace Portfolio.Repositories.Mappings;

public class FiberClientMapping : IEntityTypeConfiguration<FiberClient>
{
    public void Configure(EntityTypeBuilder<FiberClient> builder)
    {
        builder.ToTable("FiberClients");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.ContactName).HasColumnName("contact_name");
        builder.Property(x => x.Email).HasColumnName("email");
        builder.Property(x => x.Phone).HasColumnName("phone");
        builder.Property(x => x.City).HasColumnName("city");
        builder.Property(x => x.State).HasColumnName("state");
        builder.Property(x => x.Latitude).HasColumnName("latitude");
        builder.Property(x => x.Longitude).HasColumnName("longitude");
        builder.Property(x => x.CreatedDate).HasColumnName("created_date");
        builder.HasMany(x => x.Orders)
            .WithOne(x => x.Client)
            .HasForeignKey(x => x.ClientId);
    }
}
