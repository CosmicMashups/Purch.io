using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class ItemRecipeLineConfiguration : IEntityTypeConfiguration<ItemRecipeLine>
{
    public void Configure(EntityTypeBuilder<ItemRecipeLine> builder)
    {
        _ = builder.HasIndex(line => line.ItemId);
    }
}
