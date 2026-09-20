using Microsoft.EntityFrameworkCore;
using Purch.Common.TestUtilities;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;
using Purch.Infrastructure.Persistence;

namespace Purch.IntegrationTests;

/// <summary>Two requests that load the same row and both save must not both win: the second save
/// has to fail (surfacing as a 409) rather than double-record a payment or lose a stock update.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ConcurrencyTests(PostgresContainerFixture postgres)
{
    private PurchDbContext NewContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        return new PurchDbContext(options, new TestCurrentTenantProvider { TenantId = tenantId });
    }

    [Fact]
    public async Task A_cart_completed_by_two_requests_at_once_is_saved_only_once()
    {
        var tenantId = Guid.NewGuid();
        Guid cartId;
        await using (var seed = NewContext(tenantId))
        {
            var cart = new Transaction { TenantId = tenantId, BranchId = Guid.NewGuid(), DeviceId = Guid.NewGuid(), TotalAmount = 100m };
            _ = seed.Transactions.Add(cart);
            _ = await seed.SaveChangesAsync();
            cartId = cart.Id;
        }

        await using var first = NewContext(tenantId);
        await using var second = NewContext(tenantId);
        var firstCart = await first.Transactions.SingleAsync(t => t.Id == cartId);
        var secondCart = await second.Transactions.SingleAsync(t => t.Id == cartId);

        firstCart.Status = TransactionStatus.Completed;
        _ = await first.SaveChangesAsync();

        secondCart.Status = TransactionStatus.Completed;
        _ = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Two_stock_updates_from_the_same_starting_count_cannot_both_be_saved()
    {
        var tenantId = Guid.NewGuid();
        Guid itemId;
        await using (var seed = NewContext(tenantId))
        {
            var item = new Item { TenantId = tenantId, Name = "Water", BasePrice = 15m, StockOnHand = 10m };
            _ = seed.Items.Add(item);
            _ = await seed.SaveChangesAsync();
            itemId = item.Id;
        }

        await using var first = NewContext(tenantId);
        await using var second = NewContext(tenantId);
        var firstItem = await first.Items.SingleAsync(i => i.Id == itemId);
        var secondItem = await second.Items.SingleAsync(i => i.Id == itemId);

        firstItem.StockOnHand -= 3m;
        _ = await first.SaveChangesAsync();

        secondItem.StockOnHand -= 4m;
        _ = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        await using var check = NewContext(tenantId);
        Assert.Equal(7m, (await check.Items.SingleAsync(i => i.Id == itemId)).StockOnHand);
    }
}
