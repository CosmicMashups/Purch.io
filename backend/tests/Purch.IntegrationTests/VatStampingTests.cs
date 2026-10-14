using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Purch.Application.Catalog;
using Purch.Application.Pos;
using Purch.Common.TestUtilities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class VatStampingTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string MigrationBeforeVatBackfill = "20261014011918_AddU1PosFoundations";

    [Fact]
    public async Task Checkout_StampsVat()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var created = await client.PostAsJsonAsync("/items", new CreateItemRequest("Vat Item", null, null, null, 112m, null, PricingType.Unit));
        var item = (await created.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        var response = await client.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(
                Guid.NewGuid(),
                [new AddTransactionLineRequest(item.Id, null, 1m)],
                SeniorPwdDiscountApplied: false,
                PromoCode: null,
                OrderType: "Take Out",
                Payment: new RecordPaymentRequest(PaymentMethod.Cash, 112m),
                ExpectedTotal: 112m));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sale = (await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """SELECT "VatableSales","VatAmount","VatExemptSales","ZeroRatedSales","VatRateApplied","VatDerived" FROM "Transactions" WHERE "Id" = @id""",
            connection);
        command.Parameters.AddWithValue("id", sale.Id);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(100m, reader.GetDecimal(0));
        Assert.Equal(12m, reader.GetDecimal(1));
        Assert.Equal(0m, reader.GetDecimal(2));
        Assert.Equal(0m, reader.GetDecimal(3));
        Assert.Equal(0.12m, reader.GetDecimal(4));
        Assert.False(reader.GetBoolean(5));
    }

    [Fact]
    public async Task Migration_backfills_vat_for_completed_sales_with_null_stamps()
    {
        var scratchName = $"vatbackfill_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(postgres.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {scratchName}", admin);
            _ = await create.ExecuteNonQueryAsync();
        }

        var scratch = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = scratchName, Pooling = false }.ConnectionString;
        var tenantId = Guid.NewGuid();
        var regular = Guid.NewGuid();
        var senior = Guid.NewGuid();
        var open = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(scratch).Options;
        await using (var before = new PurchDbContext(options, new TestCurrentTenantProvider { TenantId = tenantId }))
        {
            await before.GetService<IMigrator>().MigrateAsync(MigrationBeforeVatBackfill);
            await using var connection = new NpgsqlConnection(scratch);
            await connection.OpenAsync();
            await InsertAsync(connection, tenantId, regular, TransactionStatus.Completed, 112m, 0m, false);
            await InsertAsync(connection, tenantId, senior, TransactionStatus.Refunded, 160m, 40m, true);
            await InsertAsync(connection, tenantId, open, TransactionStatus.Open, 112m, 0m, false);
        }

        await using var after = new PurchDbContext(options, new TestCurrentTenantProvider { TenantId = tenantId });
        await after.Database.MigrateAsync();
        var rows = await after.Transactions.IgnoreQueryFilters().Where(t => t.TenantId == tenantId).ToDictionaryAsync(t => t.Id);

        Assert.True(rows[regular].VatDerived);
        Assert.Equal(0.12m, rows[regular].VatRateApplied);
        Assert.Equal(100m, rows[regular].VatableSales);
        Assert.Equal(12m, rows[regular].VatAmount);
        Assert.Equal(0m, rows[regular].VatExemptSales);

        Assert.True(rows[senior].VatDerived);
        Assert.Equal(0m, rows[senior].VatableSales);
        Assert.Equal(0m, rows[senior].VatAmount);
        Assert.Equal(200m, rows[senior].VatExemptSales);

        Assert.False(rows[open].VatDerived);
        Assert.Null(rows[open].VatRateApplied);
    }

    private static async Task InsertAsync(NpgsqlConnection connection, Guid tenantId, Guid id, TransactionStatus status, decimal total, decimal discount, bool senior)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "Transactions"
                ("Id","TenantId","CreatedAt","BranchId","DeviceId","Status","TotalAmount","DiscountAmount",
                 "SeniorPwdDiscountApplied","PromoDiscountAmount","ItemPromoDiscountAmount","OriginatedFromKiosk","KioskPrepNumber","KitchenStatus")
            VALUES (@id,@tenantId,now(),@branch,@device,@status,@total,@discount,@senior,0,0,false,0,0)
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("tenantId", tenantId);
        command.Parameters.AddWithValue("branch", Guid.NewGuid());
        command.Parameters.AddWithValue("device", Guid.NewGuid());
        command.Parameters.AddWithValue("status", (int)status);
        command.Parameters.AddWithValue("total", total);
        command.Parameters.AddWithValue("discount", discount);
        command.Parameters.AddWithValue("senior", senior);
        _ = await command.ExecuteNonQueryAsync();
    }
}
