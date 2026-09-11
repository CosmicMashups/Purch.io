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
        _ = builder.HasIndex(d => d.PairingCode).IsUnique();
    }
}
