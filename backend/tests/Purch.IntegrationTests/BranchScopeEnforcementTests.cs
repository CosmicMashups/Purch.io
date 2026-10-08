using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

/// <summary>A manager confined to one branch may not change another branch's stock: reports already limit
/// what they can READ, and these cover the writes that take a branch id from the request.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class BranchScopeEnforcementTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record Setup(
        HttpClient Admin,
        HttpClient BranchManager,
        Guid MainBranchId,
        Guid OwnBranchId,
        ItemDto Item);

    private static async Task<Setup> SetUpAsync(PurchApiFactory factory)
    {
        var admin = await TestSessions.AdminClientAsync(factory);
        var mainBranchId = TestSessions.ShopOf(admin).Tenant.BranchId;
        var own = (await (await admin.PostAsJsonAsync("/branches", new CreateBranchRequest("Second Branch", null))).Content.ReadFromJsonAsync<BranchDto>(JsonOptions))!;

        // Staff are confined to the branch their device is in. Warehouse staff at the second branch, on that branch's own
        // Warehouse device, stand in for the branch-limited manager the old sign-in allowed.
        var staffId = await TestSessions.AddPersonAsync(admin, "Bea Branch", MembershipRole.Staff, StaffDuty.Warehouse, "5678", own.Id);
        using var anonymous = factory.CreateClient();
        var (_, credential) = await TestSessions.PairAsync(admin, anonymous, own.Id, DeviceType.WarehouseOfficer, "Second branch stock room");
        var manager = await TestSessions.UnlockAsync(admin, staffId, "5678", credential);

        var item = (await (await admin.PostAsJsonAsync("/items", new CreateItemRequest("Canned Goods", null, null, null, 30m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        _ = await admin.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item.Id, mainBranchId, MovementType.StockIn, 100m, null, null, null, null));

        return new Setup(admin, manager, mainBranchId, own.Id, item);
    }

    [Fact]
    public async Task A_branch_manager_can_record_stock_movements_for_their_own_branch_only()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await SetUpAsync(factory);
        using var _admin = setup.Admin;
        using var _manager = setup.BranchManager;

        var own = await setup.BranchManager.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(setup.Item.Id, setup.OwnBranchId, MovementType.StockIn, 5m, null, null, null, null));
        var other = await setup.BranchManager.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(setup.Item.Id, setup.MainBranchId, MovementType.StockOut, 5m, null, null, null, null));

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
    }

    [Fact]
    public async Task A_branch_manager_reading_movements_only_ever_sees_their_own_branch()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await SetUpAsync(factory);
        using var _admin = setup.Admin;
        using var _manager = setup.BranchManager;
        _ = await setup.BranchManager.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(setup.Item.Id, setup.OwnBranchId, MovementType.StockIn, 5m, null, null, null, null));

        // Naming the other branch in the query must not widen what they can read.
        var asked = await setup.BranchManager.GetFromJsonAsync<List<InventoryMovementDto>>($"/inventory/movements?branchId={setup.MainBranchId}", JsonOptions);
        var unasked = await setup.BranchManager.GetFromJsonAsync<List<InventoryMovementDto>>("/inventory/movements", JsonOptions);

        Assert.NotEmpty(asked!);
        Assert.All(asked!, m => Assert.Equal(setup.OwnBranchId, m.BranchId));
        Assert.All(unasked!, m => Assert.Equal(setup.OwnBranchId, m.BranchId));

        // The tenant-wide admin still sees the branch the manager is barred from (the seed stock-in).
        var all = await setup.Admin.GetFromJsonAsync<List<InventoryMovementDto>>("/inventory/movements", JsonOptions);
        Assert.Contains(all!, m => m.BranchId == setup.MainBranchId);
    }

    [Fact]
    public async Task A_tenant_wide_admin_is_not_confined_to_a_branch()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await SetUpAsync(factory);
        using var _admin = setup.Admin;
        using var _manager = setup.BranchManager;

        var response = await setup.Admin.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(setup.Item.Id, setup.OwnBranchId, MovementType.StockIn, 5m, null, null, null, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_branch_manager_can_ship_out_of_their_own_branch_and_not_out_of_another()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await SetUpAsync(factory);
        using var _admin = setup.Admin;
        using var _manager = setup.BranchManager;

        var fromOther = await setup.BranchManager.PostAsJsonAsync(
            "/branch-transfers",
            new CreateBranchTransferRequest(setup.MainBranchId, setup.OwnBranchId, [new CreateBranchTransferLineRequest(setup.Item.Id, 10m)]));
        Assert.Equal(HttpStatusCode.Forbidden, fromOther.StatusCode);

        var fromOwn = await setup.BranchManager.PostAsJsonAsync(
            "/branch-transfers",
            new CreateBranchTransferRequest(setup.OwnBranchId, setup.MainBranchId, [new CreateBranchTransferLineRequest(setup.Item.Id, 10m)]));
        Assert.Equal(HttpStatusCode.OK, fromOwn.StatusCode);
        var transfer = (await fromOwn.Content.ReadFromJsonAsync<BranchTransferDto>(JsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, (await setup.BranchManager.PostAsync($"/branch-transfers/{transfer.Id}/mark-in-transit", null)).StatusCode);
        // The receiving end is the main branch, so it is not theirs to confirm.
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.BranchManager.PostAsync($"/branch-transfers/{transfer.Id}/mark-received", null)).StatusCode);
        // Either end may call it off.
        Assert.Equal(HttpStatusCode.OK, (await setup.BranchManager.PostAsync($"/branch-transfers/{transfer.Id}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task A_branch_manager_cannot_order_stock_for_or_receive_stock_at_another_branch()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await SetUpAsync(factory);
        using var _admin = setup.Admin;
        using var _manager = setup.BranchManager;
        var supplier = (await (await setup.Admin.PostAsJsonAsync("/suppliers", new CreateSupplierRequest("Acme Distribution", null))).Content.ReadFromJsonAsync<SupplierDto>(JsonOptions))!;
        static PurchaseOrderDto Parse(HttpResponseMessage response)
        {
            return response.Content.ReadFromJsonAsync<PurchaseOrderDto>(JsonOptions).GetAwaiter().GetResult()!;
        }

        var forOther = await setup.BranchManager.PostAsJsonAsync(
            "/purchase-orders",
            new CreatePurchaseOrderRequest(supplier.Id, setup.MainBranchId, [new CreatePurchaseOrderLineRequest(setup.Item.Id, 10m, 5m)]));
        Assert.Equal(HttpStatusCode.Forbidden, forOther.StatusCode);

        // A purchase order for the main branch, made by the admin, is not theirs to send or cancel either.
        var mainOrder = Parse(await setup.Admin.PostAsJsonAsync(
            "/purchase-orders",
            new CreatePurchaseOrderRequest(supplier.Id, setup.MainBranchId, [new CreatePurchaseOrderLineRequest(setup.Item.Id, 10m, 5m)])));
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.BranchManager.PostAsync($"/purchase-orders/{mainOrder.Id}/mark-sent", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.BranchManager.PostAsync($"/purchase-orders/{mainOrder.Id}/cancel", null)).StatusCode);

        // Their own branch's orders work as before.
        var ownOrder = await setup.BranchManager.PostAsJsonAsync(
            "/purchase-orders",
            new CreatePurchaseOrderRequest(supplier.Id, setup.OwnBranchId, [new CreatePurchaseOrderLineRequest(setup.Item.Id, 10m, 5m)]));
        Assert.Equal(HttpStatusCode.OK, ownOrder.StatusCode);
    }

}
