using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.CreditLedger;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Application.Shifts;
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

    [Fact]
    public async Task Refunding_a_completed_sale_is_audited()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await admin.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Coffee", null, null, null, 120m, null, PricingType.Unit));
        var item = (await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        var checkoutResponse = await admin.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(
                Guid.NewGuid(),
                [new AddTransactionLineRequest(item.Id, null, 1m, null, null)],
                false, null, null,
                new RecordPaymentRequest(PaymentMethod.Cash, 120m)));
        var sale = (await checkoutResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;

        var refundResponse = await admin.PostAsJsonAsync(
            $"/transactions/{sale.Id}/refund",
            new RefundTransactionRequest("Wrong order prepared"));
        Assert.True(refundResponse.IsSuccessStatusCode);

        var logs = await admin.GetFromJsonAsync<List<AuditLogDto>>("/audit-logs", JsonOptions);
        var refundEntry = Assert.Single(logs!, log => log.ActionType == AuditActionType.Refund);
        Assert.Equal(sale.Id, refundEntry.TargetEntityId);
        Assert.Equal(nameof(Transaction), refundEntry.TargetEntityType);
    }

    [Fact]
    public async Task Discount_override_is_audited_on_sale()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await admin.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Sandwich", null, null, null, 100m, null, PricingType.Unit));
        var item = (await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        _ = await admin.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(
                Guid.NewGuid(),
                [new AddTransactionLineRequest(item.Id, null, 1m, null, null)],
                true, null, null,
                new RecordPaymentRequest(PaymentMethod.Cash, 80m)));

        var logs = await admin.GetFromJsonAsync<List<AuditLogDto>>("/audit-logs", JsonOptions);
        _ = Assert.Single(logs!, log => log.ActionType == AuditActionType.DiscountOverride);
    }

    [Fact]
    public async Task Department_reassignment_is_audited()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await AuthenticatedAdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(admin);

        var deptResponse = await admin.PostAsJsonAsync(
            $"/branches/{branchId}/departments",
            new CreateDepartmentRequest("Beverages", null));
        var dept = (await deptResponse.Content.ReadFromJsonAsync<DepartmentDto>(JsonOptions))!;

        var itemResponse = await admin.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Iced Tea", null, null, null, 45m, null, PricingType.Unit));
        var item = (await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        _ = await admin.PutAsJsonAsync(
            $"/items/{item.Id}/department",
            new UpdateItemDepartmentRequest(dept.Id));

        var logs = await admin.GetFromJsonAsync<List<AuditLogDto>>("/audit-logs", JsonOptions);
        var deptEntry = Assert.Single(logs!, log => log.ActionType == AuditActionType.DepartmentReassignment);
        Assert.Equal(item.Id, deptEntry.TargetEntityId);
        Assert.Equal(nameof(Item), deptEntry.TargetEntityType);
    }

    [Fact]
    public async Task Cash_drawer_manual_open_is_audited()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await AuthenticatedAdminClientAsync(factory);

        _ = await admin.PostAsJsonAsync("/shifts/open", new OpenShiftRequest(1000m));

        var openDrawerResponse = await admin.PostAsJsonAsync(
            "/shifts/manual-drawer-open",
            new ManualDrawerOpenRequest("Making change for 1000 peso bill"));
        Assert.True(openDrawerResponse.IsSuccessStatusCode);

        var logs = await admin.GetFromJsonAsync<List<AuditLogDto>>("/audit-logs", JsonOptions);
        var drawerEntry = Assert.Single(logs!, log => log.ActionType == AuditActionType.CashDrawerManualOpen);
        Assert.Equal(nameof(Shift), drawerEntry.TargetEntityType);
    }

    [Fact]
    public async Task Credit_limit_override_and_customer_anonymization_are_audited()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await AuthenticatedAdminClientAsync(factory);

        _ = await admin.PutAsJsonAsync(
            "/tenant/settings/credit-ledger",
            new UpdateCreditLedgerSettingRequest(true));

        var createResponse = await admin.PostAsJsonAsync(
            "/credit-ledger",
            new CreateCustomerCreditLedgerRequest("Maria Santos", "09191234567", "Makati City", 3000m, null));
        var ledger = (await createResponse.Content.ReadFromJsonAsync<CustomerCreditLedgerDto>(JsonOptions))!;

        // 1. Update/override credit limit
        _ = await admin.PutAsJsonAsync(
            $"/credit-ledger/{ledger.Id}/credit-limit",
            new UpdateCreditLimitRequest(6000m, "Customer credit score upgrade"));

        // 2. Anonymize customer (DPA RA 10173 right to erasure)
        var anonymizeResponse = await admin.PostAsync($"/credit-ledger/{ledger.Id}/anonymize", null);
        Assert.True(anonymizeResponse.IsSuccessStatusCode);
        var anonymizedLedger = (await anonymizeResponse.Content.ReadFromJsonAsync<CustomerCreditLedgerDto>(JsonOptions))!;
        Assert.Equal("[ANONYMIZED]", anonymizedLedger.CustomerFullName);
        Assert.Equal("00000000000", anonymizedLedger.CustomerPhoneNumber);
        Assert.Null(anonymizedLedger.CustomerAddress);
        Assert.False(anonymizedLedger.IsActive);

        var logs = await admin.GetFromJsonAsync<List<AuditLogDto>>("/audit-logs", JsonOptions);
        var creditLimitEntry = Assert.Single(logs!, log => log.ActionType == AuditActionType.CreditLimitOverride);
        Assert.Equal(ledger.Id, creditLimitEntry.TargetEntityId);

        var anonEntry = Assert.Single(logs!, log => log.ActionType == AuditActionType.CustomerAnonymized);
        Assert.Equal(ledger.Id, anonEntry.TargetEntityId);
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
