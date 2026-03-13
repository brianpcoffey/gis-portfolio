using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Common.Models;

namespace Portfolio.Repositories.Mappings;

public class FiberInventoryTransactionMapping : IEntityTypeConfiguration<FiberInventoryTransaction>
{
    public void Configure(EntityTypeBuilder<FiberInventoryTransaction> builder)
    {
        builder.ToTable("FiberInventoryTransactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.MaterialId).HasColumnName("material_id");
        builder.Property(x => x.TransactionType).HasColumnName("transaction_type");
        builder.Property(x => x.Quantity).HasColumnName("quantity");
        builder.Property(x => x.QtyBeforeTransaction).HasColumnName("qty_before_transaction");
        builder.Property(x => x.QtyAfterTransaction).HasColumnName("qty_after_transaction");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.TransactionDate).HasColumnName("transaction_date");
        builder.HasOne(x => x.Material)
            .WithMany(x => x.InventoryTransactions)
            .HasForeignKey(x => x.MaterialId);
    }
}
