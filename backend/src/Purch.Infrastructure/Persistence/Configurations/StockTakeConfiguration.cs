using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class StockTakeConfiguration : IEntityTypeConfiguration<StockTake>
{
    public void Configure(EntityTypeBuilder<StockTake> builder)
    {
        _ = builder.Property(t => t.Name).HasMaxLength(120);
        _ = builder.Property(t => t.Note).HasMaxLength(500);

        _ = builder.HasMany(t => t.Lines)
            .WithOne()
            .HasForeignKey(l => l.StockTakeId)
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasIndex(t => new { t.TenantId, t.Status })
            .HasDatabaseName("IX_StockTakes_TenantId_Status");
    }
}
