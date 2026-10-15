using Microsoft.EntityFrameworkCore;
using Purch.Application.Reporting;
using Purch.Common.TestUtilities;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;
using Purch.Infrastructure.Repositories;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests.Reporting;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class InventoryCogsRepositoryTests(PostgresContainerFixture postgres)
{
    private PurchDbContext NewContext(Guid tenantId) =>
        new(new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(postgres.ConnectionString).Options, new TestCurrentTenantProvider { TenantId = tenantId });

    [Fact]
    public async Task Cogs_nets_refunds_returns_and_excludes_voids_unknown_cost_and_open_items()
    {
        var tenant = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var device = Guid.NewGuid();
        var sold = new DateTimeOffset(2026, 3, 10, 4, 0, 0, TimeSpan.Zero);
        var itemId = Guid.NewGuid();
        var returnedLineId = Guid.NewGuid();

        Transaction Sale(TransactionStatus status, DateTimeOffset? parkedAt = null) => new()
        {
            TenantId = tenant, BranchId = branch, DeviceId = device, Status = status, CompletedAt = sold, ParkedAt = parkedAt,
        };
        TransactionLine Line(Transaction t, decimal qty, decimal total, decimal? cost, bool open = false, Guid? id = null) => new()
        {
            TenantId = tenant, TransactionId = t.Id, ItemId = open ? null : itemId, Quantity = qty, UnitPrice = total / qty,
            LineTotal = total, UnitCost = cost, IsOpenItem = open, OpenItemName = open ? "Misc" : null, Id = id ?? Guid.NewGuid(),
        };

        var completed = Sale(TransactionStatus.Completed);
        var refunded = Sale(TransactionStatus.Refunded);
        var voided = Sale(TransactionStatus.Voided);
        var parkedExpired = Sale(TransactionStatus.Voided, sold);
        var exchanged = Sale(TransactionStatus.Completed);
        var open = Sale(TransactionStatus.Completed);

        await using (var db = NewContext(tenant))
        {
            _ = db.Items.Add(new Item { Id = itemId, TenantId = tenant, Name = "Widget" });
            db.Transactions.AddRange(completed, refunded, voided, parkedExpired, exchanged, open);
            db.TransactionLines.AddRange(
                Line(completed, 2, 20m, 3m),
                Line(completed, 1, 10m, null),
                Line(refunded, 5, 50m, 2m),
                Line(voided, 1, 10m, 1m),
                Line(parkedExpired, 1, 10m, 1m),
                Line(exchanged, 4, 40m, 5m, id: returnedLineId),
                Line(open, 1, 50m, null, open: true));
            _ = await db.SaveChangesAsync();

            var adjustment = new Adjustment
            {
                TenantId = tenant, OriginalTransactionId = exchanged.Id, BranchId = branch, DeviceId = device,
                RequestedByUserId = Guid.NewGuid(), ApprovedByUserId = Guid.NewGuid(), Reason = "swap",
            };
            _ = db.Adjustments.Add(adjustment);
            _ = db.AdjustmentReturnLines.Add(new AdjustmentReturnLine
            {
                TenantId = tenant, AdjustmentId = adjustment.Id, OriginalLineId = returnedLineId, ItemId = itemId, Quantity = 1, UnitPrice = 10m, LineTotal = 10m,
            });
            _ = await db.SaveChangesAsync();
        }

        await using var read = NewContext(tenant);
        var service = new InventoryCogsService(
            new EfInventoryCogsRepository(read), new EfItemRepository(read), new EfCategoryRepository(read), new TestCurrentTenantProvider { TenantId = tenant });
        var result = await service.GetAsync(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), "item");

        Assert.Equal(21m, result.Cogs);
        Assert.Equal(50m, result.KnownCostRevenue);
        Assert.Equal(45.5m, result.CoveragePercent);
        Assert.Equal(2, result.ExcludedLineCount);
    }
}
