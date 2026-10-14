using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Purch.Application.Catalog;
using Purch.Application.CreditLedger;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

/// <summary>Single-tender X/Z readings must stay byte-identical once split tender exists. The golden file was captured
/// from the code before split tender was added (Task 8); only per-run values (ids, timestamps) are masked.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class SingleTenderReadingGoldenTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string GoldenPath([CallerFilePath] string here = "")
        => Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "Golden", "single-tender-xz.json");

    [Fact]
    public async Task Single_tender_x_and_z_readings_match_the_golden_file()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        _ = await admin.PutAsJsonAsync("/tenant/settings/credit-ledger", new UpdateCreditLedgerSettingRequest(true));
        var ledger = (await (await admin.PostAsJsonAsync("/credit-ledger", new CreateCustomerCreditLedgerRequest("Golden Customer", "09170000000", null, 1000m, null)))
            .Content.ReadFromJsonAsync<CustomerCreditLedgerDto>(JsonOptions))!;
        var item = (await (await admin.PostAsJsonAsync("/items", new CreateItemRequest("Golden Rice", null, null, null, 123.45m, null, PricingType.Unit)))
            .Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        async Task<TransactionDto> SellAsync(decimal quantity, RecordPaymentRequest payment)
        {
            _ = await admin.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, quantity));
            var response = await admin.PostAsJsonAsync("/transactions/cart/payments", payment);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
        }

        _ = await SellAsync(2m, new RecordPaymentRequest(PaymentMethod.Cash, 300m));
        _ = await SellAsync(1m, new RecordPaymentRequest(PaymentMethod.ManualGcashQr, null));
        _ = await SellAsync(3m, new RecordPaymentRequest(PaymentMethod.BankTransfer, null));
        var utang = await SellAsync(1m, new RecordPaymentRequest(PaymentMethod.UtangCredit, null, ledger.Id));
        var refunded = await SellAsync(1m, new RecordPaymentRequest(PaymentMethod.Cash, 200m));
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/transactions/{refunded.Id}/refund", new RefundTransactionRequest("Golden refund", TestSessions.AdminPin))).StatusCode);
        Assert.Equal(TransactionStatus.Completed, utang.Status);

        var x = await ReadingAsync(admin, "/reports/x-reading");
        var z = await ReadingAsync(admin, "/reports/z-reading");
        var actual = new JsonObject { ["x"] = x, ["z"] = z }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n");

        var path = GoldenPath();
        if (!File.Exists(path))
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, actual);
            Assert.Fail($"Golden file was missing and has been captured at {path}; re-run.");
        }

        Assert.Equal((await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n"), actual);
    }

    private static async Task<JsonNode> ReadingAsync(HttpClient client, string route)
    {
        var response = await client.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        node["generatedAt"] = "<masked>";
        node["deviceId"] = "<masked>";
        node["machineIdentificationNumber"] = "<masked>";
        return node;
    }
}
