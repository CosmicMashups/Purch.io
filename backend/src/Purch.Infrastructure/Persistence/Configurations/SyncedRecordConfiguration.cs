using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class SyncedRecordConfiguration : IEntityTypeConfiguration<SyncedRecord>
{
    public void Configure(EntityTypeBuilder<SyncedRecord> builder)
    {
        // Idempotency: the same client-generated key must never be applied twice.
        _ = builder.HasIndex(s => new { s.TenantId, s.DeviceId, s.IdempotencyKey }).IsUnique();
    }
}
