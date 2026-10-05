using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class ModifierGroupCategoryItemConfiguration : IEntityTypeConfiguration<ModifierGroupCategoryItem>
{
    public void Configure(EntityTypeBuilder<ModifierGroupCategoryItem> builder)
    {
        _ = builder.HasIndex(row => new { row.ModifierGroupId, row.ItemId }).IsUnique();
    }
}
