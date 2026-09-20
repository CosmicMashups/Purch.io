using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Purch.Common.TestUtilities;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;
using Purch.Infrastructure.Persistence;

namespace Purch.IntegrationTests;

/// <summary>A device has at most one open cart and one open shift, enforced by the database - and the
/// migration that adds that enforcement repairs any duplicates already sitting in real data first.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class OpenPerDeviceAndMigrationTests(PostgresContainerFixture postgres)
{
    private const string MigrationBeforeIndexes = "20260922000000_AddConcurrencyTokens";

    private static PurchDbContext NewContext(string connectionString, Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(connectionString).Options;
        return new PurchDbContext(options, new TestCurrentTenantProvider { TenantId = tenantId });
    }

    private static Transaction Cart(Guid tenantId, Guid deviceId, TransactionStatus status, DateTimeOffset createdAt)
    {
        return new Transaction { TenantId = tenantId, BranchId = Guid.NewGuid(), DeviceId = deviceId, Status = status, CreatedAt = createdAt };
    }

    private static Shift OpenShift(Guid tenantId, Guid deviceId, ShiftStatus status, DateTimeOffset openedAt)
    {
        return new Shift { TenantId = tenantId, BranchId = Guid.NewGuid(), DeviceId = deviceId, OpenedByUserId = Guid.NewGuid(), Status = status, OpenedAt = openedAt };
    }

    [Fact]
    public async Task A_second_open_cart_for_the_same_device_is_refused_but_finished_ones_are_fine()
    {
        var tenantId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        await using var dbContext = NewContext(postgres.ConnectionString, tenantId);
        _ = dbContext.Transactions.Add(Cart(tenantId, deviceId, TransactionStatus.Open, DateTimeOffset.UtcNow));
        _ = dbContext.Transactions.Add(Cart(tenantId, deviceId, TransactionStatus.Completed, DateTimeOffset.UtcNow));
        _ = dbContext.Transactions.Add(Cart(tenantId, deviceId, TransactionStatus.Voided, DateTimeOffset.UtcNow));
        _ = await dbContext.SaveChangesAsync();

        // Another device is unaffected.
        _ = dbContext.Transactions.Add(Cart(tenantId, Guid.NewGuid(), TransactionStatus.Open, DateTimeOffset.UtcNow));
        _ = await dbContext.SaveChangesAsync();

        _ = dbContext.Transactions.Add(Cart(tenantId, deviceId, TransactionStatus.Open, DateTimeOffset.UtcNow));
        _ = await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task A_second_open_shift_for_the_same_device_is_refused_but_closed_ones_are_fine()
    {
        var tenantId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        await using var dbContext = NewContext(postgres.ConnectionString, tenantId);
        _ = dbContext.Shifts.Add(OpenShift(tenantId, deviceId, ShiftStatus.Open, DateTimeOffset.UtcNow));
        _ = dbContext.Shifts.Add(OpenShift(tenantId, deviceId, ShiftStatus.Closed, DateTimeOffset.UtcNow.AddHours(-8)));
        _ = await dbContext.SaveChangesAsync();

        _ = dbContext.Shifts.Add(OpenShift(tenantId, deviceId, ShiftStatus.Open, DateTimeOffset.UtcNow));
        _ = await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task The_migration_keeps_the_newest_open_cart_and_shift_and_retires_the_older_duplicates()
    {
        // A scratch database, brought to the schema just before the indexes exist - the state a real
        // deployment could be in, with duplicates left by racing requests.
        var scratchName = $"migration_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(postgres.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {scratchName}", admin);
            _ = await create.ExecuteNonQueryAsync();
        }

        var scratch = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = scratchName, Pooling = false }.ConnectionString;
        var tenantId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var otherDeviceId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var before = NewContext(scratch, tenantId))
        {
            await before.GetService<IMigrator>().MigrateAsync(MigrationBeforeIndexes);

            _ = before.Transactions.Add(Cart(tenantId, deviceId, TransactionStatus.Open, now.AddMinutes(-30)));
            _ = before.Transactions.Add(Cart(tenantId, deviceId, TransactionStatus.Open, now.AddMinutes(-20)));
            _ = before.Transactions.Add(Cart(tenantId, deviceId, TransactionStatus.Open, now.AddMinutes(-10)));
            _ = before.Transactions.Add(Cart(tenantId, deviceId, TransactionStatus.Completed, now.AddMinutes(-40)));
            _ = before.Transactions.Add(Cart(tenantId, otherDeviceId, TransactionStatus.Open, now.AddMinutes(-5)));

            _ = before.Shifts.Add(OpenShift(tenantId, deviceId, ShiftStatus.Open, now.AddHours(-3)));
            _ = before.Shifts.Add(OpenShift(tenantId, deviceId, ShiftStatus.Open, now.AddHours(-1)));
            _ = before.Shifts.Add(OpenShift(tenantId, deviceId, ShiftStatus.Closed, now.AddHours(-9)));
            _ = await before.SaveChangesAsync();
        }

        await using var after = NewContext(scratch, tenantId);
        await after.Database.MigrateAsync();

        var carts = await after.Transactions.Where(t => t.DeviceId == deviceId).OrderBy(t => t.CreatedAt).ToListAsync();
        Assert.Equal(
            [TransactionStatus.Completed, TransactionStatus.Voided, TransactionStatus.Voided, TransactionStatus.Open],
            carts.Select(t => t.Status));
        Assert.Equal(TransactionStatus.Open, (await after.Transactions.SingleAsync(t => t.DeviceId == otherDeviceId)).Status);

        var shifts = await after.Shifts.Where(s => s.DeviceId == deviceId).OrderBy(s => s.OpenedAt).ToListAsync();
        Assert.Equal([ShiftStatus.Closed, ShiftStatus.Closed, ShiftStatus.Open], shifts.Select(s => s.Status));
        var retired = shifts[1];
        _ = Assert.NotNull(retired.ClosedAt);
        Assert.Contains("database migration", retired.HandoverNotes);

        // And the rule now holds: the indexes exist.
        _ = after.Transactions.Add(Cart(tenantId, deviceId, TransactionStatus.Open, now));
        _ = await Assert.ThrowsAsync<DbUpdateException>(() => after.SaveChangesAsync());
    }

    [Fact]
    public async Task Two_requests_shipping_the_same_transfer_cannot_both_be_saved()
    {
        var tenantId = Guid.NewGuid();
        Guid transferId;
        await using (var seed = NewContext(postgres.ConnectionString, tenantId))
        {
            var transfer = new BranchTransfer { TenantId = tenantId, SourceBranchId = Guid.NewGuid(), DestinationBranchId = Guid.NewGuid() };
            _ = seed.BranchTransfers.Add(transfer);
            _ = await seed.SaveChangesAsync();
            transferId = transfer.Id;
        }

        await using var first = NewContext(postgres.ConnectionString, tenantId);
        await using var second = NewContext(postgres.ConnectionString, tenantId);
        var a = await first.BranchTransfers.SingleAsync(t => t.Id == transferId);
        var b = await second.BranchTransfers.SingleAsync(t => t.Id == transferId);

        a.Status = BranchTransferStatus.InTransit;
        _ = await first.SaveChangesAsync();

        b.Status = BranchTransferStatus.InTransit;
        _ = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Two_requests_closing_the_same_shift_cannot_both_be_saved()
    {
        var tenantId = Guid.NewGuid();
        Guid shiftId;
        await using (var seed = NewContext(postgres.ConnectionString, tenantId))
        {
            var shift = OpenShift(tenantId, Guid.NewGuid(), ShiftStatus.Open, DateTimeOffset.UtcNow);
            _ = seed.Shifts.Add(shift);
            _ = await seed.SaveChangesAsync();
            shiftId = shift.Id;
        }

        await using var first = NewContext(postgres.ConnectionString, tenantId);
        await using var second = NewContext(postgres.ConnectionString, tenantId);
        var a = await first.Shifts.SingleAsync(s => s.Id == shiftId);
        var b = await second.Shifts.SingleAsync(s => s.Id == shiftId);

        a.Status = ShiftStatus.Closed;
        _ = await first.SaveChangesAsync();

        b.Status = ShiftStatus.Closed;
        _ = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Two_requests_receiving_the_same_purchase_order_line_cannot_both_be_saved()
    {
        var tenantId = Guid.NewGuid();
        Guid lineId;
        Guid orderId;
        await using (var seed = NewContext(postgres.ConnectionString, tenantId))
        {
            var order = new PurchaseOrder { TenantId = tenantId, SupplierId = Guid.NewGuid(), BranchId = Guid.NewGuid(), Status = PurchaseOrderStatus.Sent };
            var line = new PurchaseOrderLine { TenantId = tenantId, PurchaseOrderId = order.Id, ItemId = Guid.NewGuid(), QuantityOrdered = 10m };
            _ = seed.PurchaseOrders.Add(order);
            _ = seed.PurchaseOrderLines.Add(line);
            _ = await seed.SaveChangesAsync();
            orderId = order.Id;
            lineId = line.Id;
        }

        // Both requests read "0 of 10 received" and each try to add 8: without a guard the line would end up
        // at 16 of 10 and the stock counted twice.
        await using var first = NewContext(postgres.ConnectionString, tenantId);
        await using var second = NewContext(postgres.ConnectionString, tenantId);
        var a = await first.PurchaseOrderLines.SingleAsync(l => l.Id == lineId);
        var b = await second.PurchaseOrderLines.SingleAsync(l => l.Id == lineId);

        a.QuantityReceived += 8m;
        _ = await first.SaveChangesAsync();

        b.QuantityReceived += 8m;
        _ = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        // And the order itself (its status changes as it is received or cancelled) is guarded too.
        var orderA = await first.PurchaseOrders.SingleAsync(o => o.Id == orderId);
        var orderB = await second.PurchaseOrders.SingleAsync(o => o.Id == orderId);
        orderA.Status = PurchaseOrderStatus.Cancelled;
        _ = await first.SaveChangesAsync();
        orderB.Status = PurchaseOrderStatus.PartiallyReceived;
        _ = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Two_z_readings_or_receipt_numbers_from_the_same_sequence_cannot_both_be_saved()
    {
        var tenantId = Guid.NewGuid();
        Guid sequenceId;
        await using (var seed = NewContext(postgres.ConnectionString, tenantId))
        {
            var sequence = new ReceiptSequence { TenantId = tenantId, BranchId = Guid.NewGuid(), DeviceId = Guid.NewGuid() };
            _ = seed.ReceiptSequences.Add(sequence);
            _ = await seed.SaveChangesAsync();
            sequenceId = sequence.Id;
        }

        await using var first = NewContext(postgres.ConnectionString, tenantId);
        await using var second = NewContext(postgres.ConnectionString, tenantId);
        var a = await first.ReceiptSequences.SingleAsync(s => s.Id == sequenceId);
        var b = await second.ReceiptSequences.SingleAsync(s => s.Id == sequenceId);

        a.ZReadingResetCounter += 1;
        _ = await first.SaveChangesAsync();

        b.ZReadingResetCounter += 1;
        _ = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
