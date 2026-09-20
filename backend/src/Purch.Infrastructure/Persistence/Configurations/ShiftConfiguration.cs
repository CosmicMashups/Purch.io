using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        // One cash-drawer session per device at a time: two concurrent "open shift" requests must not
        // both succeed. Status 0 = ShiftStatus.Open.
        _ = builder.HasIndex(s => s.DeviceId)
            .IsUnique()
            .HasFilter("\"Status\" = 0")
            .HasDatabaseName("IX_Shifts_OneOpenShiftPerDevice");
    }
}
