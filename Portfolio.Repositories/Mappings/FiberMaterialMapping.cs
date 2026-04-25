using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Common.Models;

namespace Portfolio.Repositories.Mappings;

public class FiberMaterialMapping : IEntityTypeConfiguration<FiberMaterial>
{
    public void Configure(EntityTypeBuilder<FiberMaterial> builder)
    {
        builder.ToTable("FiberMaterials");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.Sku).HasColumnName("sku");
        builder.Property(x => x.UnitOfMeasure).HasColumnName("unit_of_measure");
        builder.Property(x => x.Category).HasColumnName("category");
        builder.Property(x => x.QtyOnHand).HasColumnName("qty_on_hand");
        builder.Property(x => x.ReorderPoint).HasColumnName("reorder_point");
        builder.Property(x => x.ReorderQty).HasColumnName("reorder_qty");
        builder.Property(x => x.UnitCost).HasColumnName("unit_cost");
        builder.Property(x => x.Supplier).HasColumnName("supplier");
        builder.Property(x => x.WarehouseLocation).HasColumnName("warehouse_location");
        builder.Property(x => x.LastUpdated).HasColumnName("last_updated");
        builder.HasMany(x => x.InventoryTransactions)
            .WithOne(x => x.Material)
            .HasForeignKey(x => x.MaterialId);
    }
}
