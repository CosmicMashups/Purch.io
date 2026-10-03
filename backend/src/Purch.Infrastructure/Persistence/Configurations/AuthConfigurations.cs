using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        // Global, not per tenant: sign-in finds the person before it knows which business they mean.
        _ = builder.HasIndex(a => a.Email).IsUnique();
        _ = builder.HasIndex(a => a.SupabaseUserId).IsUnique();
        _ = builder.Property(a => a.Email).HasMaxLength(320);
        _ = builder.Property(a => a.DisplayName).HasMaxLength(200);
    }
}

public class LocalCredentialConfiguration : IEntityTypeConfiguration<LocalCredential>
{
    public void Configure(EntityTypeBuilder<LocalCredential> builder)
    {
        _ = builder.HasIndex(c => c.Email).IsUnique();
        _ = builder.Property(c => c.Email).HasMaxLength(320);
    }
}

public class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        _ = builder.HasIndex(m => new { m.TenantId, m.AccountId }).IsUnique();
        _ = builder.HasOne(m => m.Account).WithMany().HasForeignKey(m => m.AccountId).OnDelete(DeleteBehavior.Restrict);
        _ = builder.HasMany(m => m.Branches).WithOne().HasForeignKey(b => b.MembershipId).OnDelete(DeleteBehavior.Cascade);
        _ = builder.HasIndex(m => m.LegacyUserId).HasFilter("\"LegacyUserId\" IS NOT NULL");
    }
}

public class MembershipBranchConfiguration : IEntityTypeConfiguration<MembershipBranch>
{
    public void Configure(EntityTypeBuilder<MembershipBranch> builder)
    {
        _ = builder.HasIndex(b => new { b.MembershipId, b.BranchId }).IsUnique();
    }
}

public class EnrolmentInviteConfiguration : IEntityTypeConfiguration<EnrolmentInvite>
{
    public void Configure(EntityTypeBuilder<EnrolmentInvite> builder)
    {
        // The link is opened with no session, so it is found by the hash of its token alone.
        _ = builder.HasIndex(i => i.TokenHash).IsUnique();
        _ = builder.HasIndex(i => new { i.TenantId, i.Email });
        _ = builder.Property(i => i.Email).HasMaxLength(320);
    }
}

public class CustomerDisplayStateConfiguration : IEntityTypeConfiguration<CustomerDisplayState>
{
    public void Configure(EntityTypeBuilder<CustomerDisplayState> builder)
    {
        // One row per Register: the latest thing it showed.
        _ = builder.HasIndex(s => s.RegisterDeviceId).IsUnique();
    }
}

public class DeviceCredentialConfiguration : IEntityTypeConfiguration<DeviceCredential>
{
    public void Configure(EntityTypeBuilder<DeviceCredential> builder)
    {
        _ = builder.HasIndex(c => c.CredentialHash).IsUnique();
        _ = builder.HasIndex(c => c.DeviceId);
    }
}
