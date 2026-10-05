using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class ItemModifierIngredientConfiguration : IEntityTypeConfiguration<ItemModifierIngredient>
{
    public void Configure(EntityTypeBuilder<ItemModifierIngredient> builder)
    {
        _ = builder.HasIndex(ingredient => ingredient.ItemModifierId);
    }
}
