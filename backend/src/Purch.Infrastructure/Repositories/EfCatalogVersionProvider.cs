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
            await StampAsync(dbContext.Items, cancellationToken),
            await StampAsync(dbContext.InventoryItems, cancellationToken),
            await StampAsync(dbContext.ItemRecipeLines, cancellationToken),
        };
        return Hash(parts);
    }

    public async Task<string> GetCategoriesVersionAsync(CancellationToken cancellationToken = default)
    {
        return Hash([await StampAsync(dbContext.Categories, cancellationToken)]);
    }

    public async Task<string> GetModifierGroupsVersionAsync(CancellationToken cancellationToken = default)
    {
        var parts = new List<string>
        {
            await StampAsync(dbContext.ModifierGroups, cancellationToken),
            await StampAsync(dbContext.ItemModifiers, cancellationToken),
        };
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

    private static string Hash(IEnumerable<string> parts)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', parts)));
        return Convert.ToHexString(bytes, 0, 12);
    }
}
