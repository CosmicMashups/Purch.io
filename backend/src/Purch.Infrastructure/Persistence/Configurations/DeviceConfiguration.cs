using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        // Global uniqueness (not per-tenant): the anonymous login flow looks up a
        // device by pairing code alone, before it knows which tenant to scope to.
        // Devices paired with a one-time code have no permanent code, so the empty value is left out of the index.
        _ = builder.HasIndex(d => d.PairingCode).IsUnique().HasFilter("\"PairingCode\" <> ''");
        _ = builder.HasIndex(d => d.PairingCodeHash).IsUnique().HasFilter("\"PairingCodeHash\" IS NOT NULL");
    }
}
