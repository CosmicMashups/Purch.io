using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        // A partial unique index (Postgres supports a filtered index via HasFilter) so
        // multiple items can still have a null barcode without colliding with each other.
        _ = builder.HasIndex(item => new { item.TenantId, item.Barcode })
            .IsUnique()
            .HasFilter("\"Barcode\" IS NOT NULL");

        // Catalog listings and category filters.
        _ = builder.HasIndex(item => new { item.TenantId, item.CategoryId })
            .HasDatabaseName("IX_Items_TenantCategory");
    }
}
