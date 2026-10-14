using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class TransactionLineConfiguration : IEntityTypeConfiguration<TransactionLine>
{
    public void Configure(EntityTypeBuilder<TransactionLine> builder)
    {
        _ = builder.Property(l => l.UnitCost).HasPrecision(18, 2);
        _ = builder.Property(l => l.ManualDiscountAmount).HasPrecision(18, 2);
        _ = builder.Property(l => l.PriceOverrideFrom).HasPrecision(18, 2);

        _ = builder.HasIndex(l => l.TransactionId)
            .HasDatabaseName("IX_TransactionLines_TransactionId");
    }
}
