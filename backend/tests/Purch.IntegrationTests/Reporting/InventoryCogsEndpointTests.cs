using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Reporting;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests.Reporting;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class InventoryCogsEndpointTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Roles_and_range_limit_are_enforced()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var cashier = await TestSessions.CashierClientAsync(admin);
        using var manager = await TestSessions.ManagerClientAsync(admin);
        using var warehouse = await TestSessions.StaffClientAsync(admin, "Wes Warehouse", MembershipRole.Staff, StaffDuty.Warehouse, "456712");

        const string ok = "/reports/inventory/cogs?from=2026-03-01&to=2026-03-31&groupBy=item";
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync(ok)).StatusCode);

        var response = await manager.GetAsync(ok);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cogs = await response.Content.ReadFromJsonAsync<InventoryCogsDto>(JsonOptions);
        Assert.Equal(0m, cogs!.Cogs);

        Assert.Equal(HttpStatusCode.OK, (await warehouse.GetAsync(ok)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/reports/inventory/cogs?from=2024-01-01&to=2026-03-31")).StatusCode);
    }
}
