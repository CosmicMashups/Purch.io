using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        var comparer = new ValueComparer<List<SupplierContact>>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null).GetHashCode(),
            list => JsonSerializer.Deserialize<List<SupplierContact>>(JsonSerializer.Serialize(list, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);

        builder.Property(supplier => supplier.Contacts)
            .HasConversion(
                list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<SupplierContact>>(json, (JsonSerializerOptions?)null) ?? new List<SupplierContact>())
            .HasColumnType("text")
            .Metadata.SetValueComparer(comparer);
    }
}
