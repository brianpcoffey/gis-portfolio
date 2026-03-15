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
        builder.Property(x => x.OrderNumber).HasColumnName("order_number");
        builder.Property(x => x.ClientName).HasColumnName("client_name");
        builder.Property(x => x.ProductName).HasColumnName("product_name");
        builder.Property(x => x.Quantity).HasColumnName("quantity");
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.OrderDate).HasColumnName("order_date");
        builder.Property(x => x.ShipDate).HasColumnName("ship_date");
        builder.Property(x => x.ClientLat).HasColumnName("client_lat");
        builder.Property(x => x.ClientLng).HasColumnName("client_lng");
        builder.HasMany(x => x.Shipments);
    }
}
