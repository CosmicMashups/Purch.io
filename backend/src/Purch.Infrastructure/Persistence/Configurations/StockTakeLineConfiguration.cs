using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class StockTakeLineConfiguration : IEntityTypeConfiguration<StockTakeLine>
{
    public void Configure(EntityTypeBuilder<StockTakeLine> builder)
    {
        _ = builder.Property(l => l.Name).HasMaxLength(200);

        // Exactly one of item / ingredient, and each appears at most once per take.
        _ = builder.ToTable(t => t.HasCheckConstraint(
            "CK_StockTakeLines_ExactlyOneTarget",
            "(\"ItemId\" IS NULL) <> (\"InventoryItemId\" IS NULL)"));

        _ = builder.HasIndex(l => new { l.StockTakeId, l.ItemId })
            .IsUnique()
            .HasDatabaseName("IX_StockTakeLines_StockTakeId_ItemId");

        _ = builder.HasIndex(l => new { l.StockTakeId, l.InventoryItemId })
            .IsUnique()
            .HasDatabaseName("IX_StockTakeLines_StockTakeId_InventoryItemId");
    }
}
