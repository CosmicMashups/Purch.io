using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        _ = builder.Property(b => b.IdempotencyKey).HasMaxLength(100);
        _ = builder.Property(b => b.ContentHash).HasMaxLength(64);
        _ = builder.Property(b => b.ParsedJson).HasColumnType("jsonb");
        _ = builder.Property(b => b.PlanJson).HasColumnType("jsonb");
        _ = builder.Property(b => b.ResolutionsJson).HasColumnType("jsonb");
        _ = builder.Property(b => b.ResultJson).HasColumnType("jsonb");

        _ = builder.HasIndex(b => new { b.TenantId, b.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("IX_ImportBatches_TenantId_IdempotencyKey");
    }
}
