using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class ReceiptSequenceConfiguration : IEntityTypeConfiguration<ReceiptSequence>
{
    public void Configure(EntityTypeBuilder<ReceiptSequence> builder)
    {
        // One independently-sequential series per (tenant, branch, device) — per Machine
        // Identification Number, not one company-wide counter. See docs/ARCHITECTURE.md §8.
        _ = builder.HasIndex(r => new { r.TenantId, r.BranchId, r.DeviceId }).IsUnique();
    }
}
