using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Global uniqueness (not per-tenant), filtered to rows that actually have an
        // email: the admin email+password login looks a user up by email alone,
        // before it knows which tenant to scope to (mirrors DeviceConfiguration's
        // pairing-code index). Most users have no email at all (PIN-only staff).
        _ = builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("\"Email\" IS NOT NULL");
    }
}
