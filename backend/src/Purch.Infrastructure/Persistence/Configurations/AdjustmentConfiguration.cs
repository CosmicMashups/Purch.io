using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class AdjustmentConfiguration : IEntityTypeConfiguration<Adjustment>
{
    public void Configure(EntityTypeBuilder<Adjustment> builder)
    {
        _ = builder.HasIndex(a => a.OriginalTransactionId)
            .HasDatabaseName("IX_Adjustments_OriginalTransactionId");
    }
}

public class AdjustmentReturnLineConfiguration : IEntityTypeConfiguration<AdjustmentReturnLine>
{
    public void Configure(EntityTypeBuilder<AdjustmentReturnLine> builder)
    {
        _ = builder.HasIndex(l => l.AdjustmentId)
            .HasDatabaseName("IX_AdjustmentReturnLines_AdjustmentId");

        _ = builder.HasIndex(l => l.OriginalLineId)
            .HasDatabaseName("IX_AdjustmentReturnLines_OriginalLineId");
    }
}

public class AdjustmentReplacementLineConfiguration : IEntityTypeConfiguration<AdjustmentReplacementLine>
{
    public void Configure(EntityTypeBuilder<AdjustmentReplacementLine> builder)
    {
        _ = builder.HasIndex(l => l.AdjustmentId)
            .HasDatabaseName("IX_AdjustmentReplacementLines_AdjustmentId");
    }
}
