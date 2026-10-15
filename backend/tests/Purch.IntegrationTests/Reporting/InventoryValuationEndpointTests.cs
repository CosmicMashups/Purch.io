using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Application.Reporting;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests.Reporting;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class InventoryValuationEndpointTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Cashier_is_forbidden_manager_and_warehouse_are_served()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);

        var branches = await admin.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions);
        var item = await (await admin.PostAsJsonAsync("/items", new CreateItemRequest("Rice", null, null, null, 55m, null, PricingType.Unit, CostPrice: 10m)))
            .Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await admin.PostAsJsonAsync("/inventory/movements", new RecordMovementRequest(item!.Id, branches!.Single().Id, MovementType.StockIn, 4m, null, null, null, null));

        using var cashier = await TestSessions.CashierClientAsync(admin);
        using var manager = await TestSessions.ManagerClientAsync(admin);
        using var warehouse = await TestSessions.StaffClientAsync(admin, "Wes Warehouse", MembershipRole.Staff, StaffDuty.Warehouse, "456712");

        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync("/reports/inventory/valuation")).StatusCode);

        var managerResponse = await manager.GetAsync("/reports/inventory/valuation");
        Assert.Equal(HttpStatusCode.OK, managerResponse.StatusCode);
        var valuation = await managerResponse.Content.ReadFromJsonAsync<InventoryValuationDto>(JsonOptions);
        Assert.Equal(40m, valuation!.TotalValue);
        Assert.Equal(1, valuation.ValuedCount);

        Assert.Equal(HttpStatusCode.OK, (await warehouse.GetAsync("/reports/inventory/valuation")).StatusCode);
    }
}
