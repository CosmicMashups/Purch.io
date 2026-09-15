using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        _ = builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        _ = builder.Property(t => t.BrandingBackgroundColorHex).HasMaxLength(7);
        _ = builder.Property(t => t.BrandingAccentColorHex).HasMaxLength(7);
        _ = builder.Property(t => t.BrandingPrimaryTextColorHex).HasMaxLength(7);
        _ = builder.Property(t => t.BrandingSecondaryTextColorHex).HasMaxLength(7);
        _ = builder.Property(t => t.Tin).HasMaxLength(20);
    }
}
