using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Common.Models;

namespace Portfolio.Repositories.Mappings;

public class FiberOrderMapping : IEntityTypeConfiguration<FiberOrder>
{
    public void Configure(EntityTypeBuilder<FiberOrder> builder)
    {
        builder.ToTable("FiberOrders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.ClientId).HasColumnName("client_id");
        builder.Property(x => x.ProductName).HasColumnName("product_name");
        builder.Property(x => x.ProductSku).HasColumnName("product_sku");
        builder.Property(x => x.Quantity).HasColumnName("quantity");
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price");
        builder.Property(x => x.TotalValue).HasColumnName("total_value");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.OrderDate).HasColumnName("order_date");
        builder.Property(x => x.ShipDate).HasColumnName("ship_date");
        builder.HasOne(x => x.Client)
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.ClientId);
        builder.HasMany(x => x.Shipments)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId);
    }
}
