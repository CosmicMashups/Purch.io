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

        // Matches ReceiptSequence's own per-(branch,device) uniqueness.
        _ = builder.HasIndex(t => new { t.TenantId, t.BranchId, t.DeviceId, t.ReceiptNumber }).IsUnique();
    }
}
