using Microsoft.EntityFrameworkCore;
using Purch.Common.TestUtilities;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;
using Purch.Infrastructure.Persistence;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class TenantIsolationTests(PostgresContainerFixture postgres)
{
    /// <summary>
    /// NFR14: a shared-database, multi-tenant architecture must never leak one tenant's
    /// rows into another tenant's query results. This proves PurchDbContext's global query
    /// filter — the single enforcement point — actually holds against a real Postgres.
    /// </summary>
    [Fact]
    public async Task Items_query_only_returns_rows_for_the_current_tenant()
    {
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();

        await SeedItemAsync(tenantAId, "Tenant A Item");
        await SeedItemAsync(tenantBId, "Tenant B Item");

        var tenantAProvider = new TestCurrentTenantProvider { TenantId = tenantAId };
        await using var scopedDbContext = CreateDbContext(tenantAProvider);

        var visibleItems = await scopedDbContext.Items.ToListAsync();

        _ = Assert.Single(visibleItems);
        Assert.Equal("Tenant A Item", visibleItems[0].Name);
        Assert.All(visibleItems, item => Assert.Equal(tenantAId, item.TenantId));
    }

    [Fact]
    public async Task Items_query_with_no_current_tenant_sees_nothing()
    {
        // Fail closed: no tenant in context must never mean "every tenant". The few reads that
        // legitimately span tenants (pairing code, email, refresh/reset tokens) opt out explicitly.
        var tenantId = Guid.NewGuid();
        await SeedItemAsync(tenantId, "Hidden Item");

        var unscopedProvider = new TestCurrentTenantProvider { TenantId = null };
        await using var dbContext = CreateDbContext(unscopedProvider);

        Assert.Empty(await dbContext.Items.Where(item => item.TenantId == tenantId).ToListAsync());
        Assert.Empty(await dbContext.Items.ToListAsync());
    }

    [Fact]
    public async Task An_explicit_IgnoreQueryFilters_is_the_only_way_to_read_across_tenants()
    {
        var tenantId = Guid.NewGuid();
        await SeedItemAsync(tenantId, "Opt-in Visible Item");

        await using var dbContext = CreateDbContext(new TestCurrentTenantProvider { TenantId = null });

        _ = Assert.Single(await dbContext.Items.IgnoreQueryFilters().Where(item => item.TenantId == tenantId).ToListAsync());
    }

    private async Task SeedItemAsync(Guid tenantId, string itemName)
    {
        var seedProvider = new TestCurrentTenantProvider { TenantId = tenantId };
        await using var dbContext = CreateDbContext(seedProvider);

        _ = dbContext.Items.Add(new Item
        {
            TenantId = tenantId,
            Name = itemName,
            BasePrice = 10m,
            PricingType = PricingType.Unit,
        });

        _ = await dbContext.SaveChangesAsync();
    }

    private PurchDbContext CreateDbContext(TestCurrentTenantProvider tenantProvider)
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;

        return new PurchDbContext(options, tenantProvider);
    }
}
