using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class ItemModifierGroupConfiguration : IEntityTypeConfiguration<ItemModifierGroup>
{
    public void Configure(EntityTypeBuilder<ItemModifierGroup> builder)
    {
        _ = builder.HasIndex(link => new { link.ItemId, link.ModifierGroupId }).IsUnique();
    }
}
