using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        _ = builder.HasIndex(m => new { m.TenantId, m.BranchId, m.ItemId, m.CreatedAt })
            .HasDatabaseName("IX_InventoryMovements_Branch_Item_CreatedAt");
    }
}
