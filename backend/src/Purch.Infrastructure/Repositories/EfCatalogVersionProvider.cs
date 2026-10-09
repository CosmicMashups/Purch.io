using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Domain.Common;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

/// <summary>Version = a hash of (row count, newest UpdatedAt) per source table. The count catches hard deletes,
/// which leave no UpdatedAt behind; PurchDbContext stamps UpdatedAt on every insert and update. The tenant
/// query filter scopes every aggregate to the caller's tenant.</summary>
public sealed class EfCatalogVersionProvider(PurchDbContext dbContext, ICurrentTenantProvider currentTenantProvider)
    : ICatalogVersionProvider
{
    public async Task<string> GetItemsVersionAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = currentTenantProvider.TenantId
            ?? throw new InvalidOperationException("The catalog version requires an authenticated tenant context.");

        var separateTracking = await dbContext.Tenants
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => tenant.UseSeparateInventoryTracking)
            .FirstOrDefaultAsync(cancellationToken);

        var parts = new List<string>
        {
            separateTracking ? "sep" : "single",
        };
        parts.AddRange(await StampsAsync(
            cancellationToken,
            Rows("items", dbContext.Items),
            Rows("inventory", dbContext.InventoryItems),
            Rows("recipes", dbContext.ItemRecipeLines),
            // An item is out of stock while equipment it needs is out of service.
            Rows("equipment", dbContext.EquipmentItems),
            Rows("itemEquipment", dbContext.ItemEquipmentLinks)));
        return Hash(parts);
    }

    public async Task<string> GetCategoriesVersionAsync(CancellationToken cancellationToken = default)
    {
        return Hash([await StampAsync(dbContext.Categories, cancellationToken)]);
    }

    public async Task<string> GetModifierGroupsVersionAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = currentTenantProvider.TenantId
            ?? throw new InvalidOperationException("The catalog version requires an authenticated tenant context.");

        var separateTracking = await dbContext.Tenants
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => tenant.UseSeparateInventoryTracking)
            .FirstOrDefaultAsync(cancellationToken);

        // A modifier's availability comes from its ingredients' stock, which changes with every sale, so the
        // version has to move with inventory as well or a client would keep showing a sold-out option as free.
        var parts = new List<string>
        {
            separateTracking ? "sep" : "single",
        };
        parts.AddRange(await StampsAsync(
            cancellationToken,
            Rows("groups", dbContext.ModifierGroups),
            Rows("modifiers", dbContext.ItemModifiers),
            Rows("ingredients", dbContext.ItemModifierIngredients),
            Rows("inventory", dbContext.InventoryItems),
            // Category-linked groups list the category's items live, with their prices and stock.
            Rows("categoryItems", dbContext.ModifierGroupCategoryItems),
            Rows("items", dbContext.Items),
            Rows("recipes", dbContext.ItemRecipeLines),
            Rows("equipment", dbContext.EquipmentItems),
            Rows("itemEquipment", dbContext.ItemEquipmentLinks)));
        return Hash(parts);
    }

    private static async Task<string> StampAsync<TEntity>(IQueryable<TEntity> source, CancellationToken cancellationToken)
        where TEntity : Entity
    {
        var stamp = await source
            .GroupBy(_ => 1)
            .Select(group => new { Count = group.Count(), Newest = group.Max(row => row.UpdatedAt ?? row.CreatedAt) })
            .FirstOrDefaultAsync(cancellationToken);

        return stamp is null ? "0" : $"{stamp.Count}@{stamp.Newest.UtcTicks}";
    }

    private sealed class StampRow
    {
        public string Tag { get; init; } = "";

        public DateTimeOffset At { get; init; }
    }

    private static IQueryable<StampRow> Rows<TEntity>(string tag, IQueryable<TEntity> source)
        where TEntity : Entity
    {
        return source.Select(row => new StampRow { Tag = tag, At = row.UpdatedAt ?? row.CreatedAt });
    }

    /// <summary>The same (count, newest) stamp per source as <see cref="StampAsync"/>, but all sources in one
    /// UNION ALL query — this runs on every catalog poll, and each extra query is a database round trip.</summary>
    private static async Task<IEnumerable<string>> StampsAsync(CancellationToken cancellationToken, params IQueryable<StampRow>[] sources)
    {
        var all = sources[0];
        for (var i = 1; i < sources.Length; i++)
        {
            all = all.Concat(sources[i]);
        }

        var stamps = (await all
            .GroupBy(row => row.Tag)
            .Select(group => new { Tag = group.Key, Count = group.Count(), Newest = group.Max(row => row.At) })
            .ToListAsync(cancellationToken))
            .ToDictionary(stamp => stamp.Tag);

        // An empty source has no group, so it simply has no part; its first row changes the version.
        return stamps.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value.Count}@{pair.Value.Newest.UtcTicks}");
    }

    private static string Hash(IEnumerable<string> parts)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', parts)));
        return Convert.ToHexString(bytes, 0, 12);
    }
}
