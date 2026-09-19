using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        _ = builder.Property(t => t.TotalAmount).HasPrecision(12, 2);
        _ = builder.Property(t => t.DiscountAmount).HasPrecision(12, 2);

        // Matches ReceiptSequence's own per-(branch,device) uniqueness. Filtered to
        // issued receipts only — Postgres already treats NULLs as distinct for a
        // unique index, but this is explicit about the intent and matches
        // ReceiptNumber now being nullable rather than a 0-sentinel.
        _ = builder.HasIndex(t => new { t.TenantId, t.BranchId, t.DeviceId, t.ReceiptNumber })
            .IsUnique()
            .HasFilter("\"ReceiptNumber\" IS NOT NULL");

        // One transaction per client-generated sale id — the idempotency guard for
        // checkout retries, including two concurrent requests carrying the same id.
        _ = builder.HasIndex(t => new { t.TenantId, t.ClientSaleId })
            .IsUnique()
            .HasFilter("\"ClientSaleId\" IS NOT NULL");
    }
}
