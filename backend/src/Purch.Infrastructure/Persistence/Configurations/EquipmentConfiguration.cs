using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        _ = builder.Property(equipment => equipment.Name).HasMaxLength(200);
        _ = builder.Property(equipment => equipment.Location).HasMaxLength(200);
        _ = builder.Property(equipment => equipment.Notes).HasMaxLength(2000);
        _ = builder.HasIndex(equipment => equipment.TenantId);
    }
}

public class ItemEquipmentConfiguration : IEntityTypeConfiguration<ItemEquipment>
{
    public void Configure(EntityTypeBuilder<ItemEquipment> builder)
    {
        _ = builder.HasIndex(link => new { link.TenantId, link.ItemId });
        _ = builder.HasIndex(link => link.EquipmentId);
        _ = builder.HasIndex(link => new { link.ItemId, link.EquipmentId }).IsUnique();
    }
}
