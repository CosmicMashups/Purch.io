using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class ExpandedAuditCoverageTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Changing_a_staff_members_role_is_audited_but_a_no_op_save_is_not()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await AuthenticatedAdminClientAsync(factory);

        _ = await admin.PostAsJsonAsync(
            "/staff",
            new CreateStaffRequest("Cash Ier", Role.Cashier, ScopeType.Tenant, null, null, "5678"));
        var staff = await admin.GetFromJsonAsync<List<StaffDto>>("/staff", JsonOptions);
        var cashier = staff!.Single(s => s.Role == Role.Cashier);

        // A save that changes nothing must not add a spurious entry.
        _ = await admin.PutAsJsonAsync(
            $"/staff/{cashier.Id}",
            new UpdateStaffRequest(Role.Cashier, ScopeType.Tenant, null, null, true));

        var beforePromotion = await admin.GetFromJsonAsync<List<AuditLogDto>>("/audit-logs", JsonOptions);
        Assert.DoesNotContain(beforePromotion!, log => log.ActionType == AuditActionType.StaffAccessChanged);

        _ = await admin.PutAsJsonAsync(
            $"/staff/{cashier.Id}",
            new UpdateStaffRequest(Role.Manager, ScopeType.Tenant, null, null, true));

        var afterPromotion = await admin.GetFromJsonAsync<List<AuditLogDto>>("/audit-logs", JsonOptions);
        var entry = Assert.Single(afterPromotion!, log => log.ActionType == AuditActionType.StaffAccessChanged);
        Assert.Equal(cashier.Id, entry.TargetEntityId);
        Assert.Equal(nameof(User), entry.TargetEntityType);
    }

    [Fact]
    public async Task Changing_an_items_price_is_audited_but_an_unrelated_edit_is_not()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await admin.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Bottled Water", null, null, null, 15m, null, PricingType.Unit));
        var item = (await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        // Renaming only must not add a price-change entry.
        _ = await admin.PutAsJsonAsync(
            $"/items/{item.Id}",
            new UpdateItemRequest("Bottled Water (Large)", null, null, null, 15m, null, true));

        var beforePriceChange = await admin.GetFromJsonAsync<List<AuditLogDto>>("/audit-logs", JsonOptions);
        Assert.DoesNotContain(beforePriceChange!, log => log.ActionType == AuditActionType.CatalogPriceChanged);

        _ = await admin.PutAsJsonAsync(
            $"/items/{item.Id}",
            new UpdateItemRequest("Bottled Water (Large)", null, null, null, 20m, null, true));

        var afterPriceChange = await admin.GetFromJsonAsync<List<AuditLogDto>>("/audit-logs", JsonOptions);
        var entry = Assert.Single(afterPriceChange!, log => log.ActionType == AuditActionType.CatalogPriceChanged);
        Assert.Equal(item.Id, entry.TargetEntityId);
    }

    [Fact]
    public async Task A_hand_entered_stock_adjustment_is_audited_but_a_stock_in_is_not()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await AuthenticatedAdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(admin);

        var itemResponse = await admin.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Canned Goods", null, null, null, 30m, null, PricingType.Unit));
        var item = (await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        _ = await admin.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item.Id, branchId, MovementType.StockIn, 50m, null, null, null, null));

        var beforeAdjustment = await admin.GetFromJsonAsync<List<AuditLogDto>>("/audit-logs", JsonOptions);
        Assert.DoesNotContain(beforeAdjustment!, log => log.ActionType == AuditActionType.InventoryAdjustment);

        _ = await admin.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item.Id, branchId, MovementType.Adjustment, -3m, "Cycle count correction", null, null, null));

        var afterAdjustment = await admin.GetFromJsonAsync<List<AuditLogDto>>("/audit-logs", JsonOptions);
        var entry = Assert.Single(afterAdjustment!, log => log.ActionType == AuditActionType.InventoryAdjustment);
        Assert.Equal(item.Id, entry.TargetEntityId);
    }

    private static async Task<Guid> MainBranchIdAsync(HttpClient client)
    {
        var branches = await client.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions);
        return branches!.Single().Id;
    }

    private static async Task<HttpClient> AuthenticatedAdminClientAsync(PurchApiFactory factory)
    {
        var client = factory.CreateClient();

        var bootstrapResponse = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest(
                $"Tenant-{Guid.NewGuid():N}",
                BusinessType.ConvenienceStore,
                "Main Branch",
                "Admin User",
                "1234"));
        var bootstrapResult = await bootstrapResponse.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions);

        var loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(bootstrapResult!.DevicePairingCode, "1234"));
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);
        return client;
    }

    private sealed record LoginResponseBody(string AccessToken);
}
