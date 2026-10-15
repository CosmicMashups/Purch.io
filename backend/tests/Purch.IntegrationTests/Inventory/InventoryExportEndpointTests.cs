using System.Net;
using Purch.Common.TestUtilities;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests.Inventory;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class InventoryExportEndpointTests(PostgresContainerFixture postgres)
{
    private static readonly string[] Routes = ["items.csv", "ingredients.csv", "stock.csv"];

    [Fact]
    public async Task Cashier_is_refused_and_manager_gets_csv()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var cashier = await TestSessions.CashierClientAsync(admin);
        using var manager = await TestSessions.ManagerClientAsync(admin);

        foreach (var route in Routes)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync($"/inventory/export/{route}")).StatusCode);
            var response = await manager.GetAsync($"/inventory/export/{route}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.StartsWith("text/csv", response.Content.Headers.ContentType!.MediaType);
        }
    }
}
