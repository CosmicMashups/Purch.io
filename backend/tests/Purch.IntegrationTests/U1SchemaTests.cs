using Microsoft.EntityFrameworkCore;
using Purch.Common.TestUtilities;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class U1SchemaTests(PostgresContainerFixture postgres)
{
    private PurchDbContext NewContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        return new PurchDbContext(options, new TestCurrentTenantProvider { TenantId = tenantId });
    }

    [Fact]
    public async Task NewColumnsRoundTrip()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var parkedAt = DateTimeOffset.UtcNow;
        var transaction = new Transaction
        {
            TenantId = tenantId,
            BranchId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            Status = TransactionStatus.Parked,
            ParkedLabel = "Table 4",
            ParkedAt = parkedAt,
            ParkedByUserId = userId,
            CustomerId = customerId,
            ManualDiscountAmount = 5.25m,
            ManualDiscountReason = "regular",
            VatableSales = 100m,
            VatAmount = 12m,
            VatExemptSales = 3m,
            ZeroRatedSales = 4m,
            VatRateApplied = 0.12m,
            VatDerived = true,
        };
        var line = new TransactionLine
        {
            TenantId = tenantId,
            TransactionId = transaction.Id,
            ItemId = null,
            Quantity = 1,
            UnitPrice = 20m,
            LineTotal = 20m,
            UnitCost = 12.5m,
            ManualDiscountAmount = 1m,
            PriceOverrideFrom = 25m,
            IsOpenItem = true,
            OpenItemName = "Misc",
            Note = "no ice",
        };
        var first = new Payment { TenantId = tenantId, TransactionId = transaction.Id, Method = PaymentMethod.Cash, Amount = 10m, Sequence = 0 };
        var second = new Payment { TenantId = tenantId, TransactionId = transaction.Id, Method = PaymentMethod.Cash, Amount = 10m, Sequence = 1, Reference = "REF1" };

        await using (var write = NewContext(tenantId))
        {
            _ = write.Transactions.Add(transaction);
            _ = write.TransactionLines.Add(line);
            write.Payments.AddRange(first, second);
            _ = await write.SaveChangesAsync();
        }

        await using var read = NewContext(tenantId);
        var loaded = await read.Transactions.AsNoTracking().SingleAsync(t => t.Id == transaction.Id);
        Assert.Equal(TransactionStatus.Parked, loaded.Status);
        Assert.Equal("Table 4", loaded.ParkedLabel);
        Assert.Equal(userId, loaded.ParkedByUserId);
        Assert.Equal(customerId, loaded.CustomerId);
        Assert.NotNull(loaded.ParkedAt);
        Assert.Equal(5.25m, loaded.ManualDiscountAmount);
        Assert.Equal("regular", loaded.ManualDiscountReason);
        Assert.Equal(100m, loaded.VatableSales);
        Assert.Equal(12m, loaded.VatAmount);
        Assert.Equal(3m, loaded.VatExemptSales);
        Assert.Equal(4m, loaded.ZeroRatedSales);
        Assert.Equal(0.12m, loaded.VatRateApplied);
        Assert.True(loaded.VatDerived);

        var loadedLine = await read.TransactionLines.AsNoTracking().SingleAsync(l => l.TransactionId == transaction.Id);
        Assert.Null(loadedLine.ItemId);
        Assert.Equal(12.5m, loadedLine.UnitCost);
        Assert.Equal(1m, loadedLine.ManualDiscountAmount);
        Assert.Equal(25m, loadedLine.PriceOverrideFrom);
        Assert.True(loadedLine.IsOpenItem);
        Assert.Equal("Misc", loadedLine.OpenItemName);
        Assert.Equal("no ice", loadedLine.Note);

        var payments = await read.Payments.AsNoTracking().Where(p => p.TransactionId == transaction.Id).OrderBy(p => p.Sequence).ToListAsync();
        Assert.Equal([0, 1], payments.Select(p => p.Sequence));
        Assert.Equal("REF1", payments[1].Reference);
    }
}
