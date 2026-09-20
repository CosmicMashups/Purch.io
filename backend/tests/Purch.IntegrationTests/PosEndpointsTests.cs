using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Application.Promotions;
using Purch.Application.Reporting;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class PosEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Getting_the_cart_creates_an_empty_open_cart_on_first_request()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var response = await client.GetAsync("/transactions/cart");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cart = await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(TransactionStatus.Open, cart!.Status);
        Assert.Empty(cart.Lines);
        Assert.Equal(0m, cart.TotalAmount);
    }

    [Fact]
    public async Task Getting_the_cart_twice_returns_the_same_open_transaction()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var first = await client.GetFromJsonAsync<TransactionDto>("/transactions/cart", JsonOptions);
        var second = await client.GetFromJsonAsync<TransactionDto>("/transactions/cart", JsonOptions);

        Assert.Equal(first!.Id, second!.Id);
    }

    [Fact]
    public async Task Adding_a_line_computes_the_total_and_adding_the_same_item_again_merges_quantity()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Bottled Water", null, null, null, 15m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var firstAdd = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 2m));
        Assert.Equal(HttpStatusCode.OK, firstAdd.StatusCode);

        var secondAdd = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item.Id, null, 3m));
        var cart = await secondAdd.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        var line = Assert.Single(cart!.Lines);
        Assert.Equal(5m, line.Quantity);
        Assert.Equal(75m, line.LineTotal);
        Assert.Equal(75m, cart.TotalAmount);
    }

    [Fact]
    public async Task Adding_a_plain_item_after_a_modified_one_keeps_them_as_separate_lines_at_their_own_prices()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Iced Latte", null, null, null, 100m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var groupResponse = await client.PostAsJsonAsync("/modifier-groups", new CreateModifierGroupRequest("Add-ons", true, false));
        var group = await groupResponse.Content.ReadFromJsonAsync<ModifierGroupDto>(JsonOptions);
        var modifierResponse = await client.PostAsJsonAsync($"/modifier-groups/{group!.Id}/modifiers", new CreateItemModifierRequest("Extra Shot", 20m));
        var modifier = (await modifierResponse.Content.ReadFromJsonAsync<ModifierGroupDto>(JsonOptions))!.Modifiers[0];
        _ = await client.PostAsJsonAsync($"/items/{item!.Id}/modifier-groups", new AttachModifierGroupRequest(group.Id));

        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item.Id, null, 1m, SelectedModifierIds: [modifier.Id]));
        var plainAdd = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item.Id, null, 1m));
        var cart = await plainAdd.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.Equal(2, cart!.Lines.Count);
        Assert.Equal(220m, cart.TotalAmount);
    }

    [Fact]
    public async Task Updating_a_line_quantity_recomputes_the_total()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Instant Coffee", null, null, null, 8m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var addResponse = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));
        var cartAfterAdd = await addResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        var lineId = cartAfterAdd!.Lines.Single().Id;

        var updateResponse = await client.PutAsJsonAsync(
            $"/transactions/cart/lines/{lineId}",
            new UpdateTransactionLineRequest(4m));
        var updated = await updateResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.Equal(32m, updated!.TotalAmount);
    }

    [Fact]
    public async Task Removing_a_line_recomputes_the_total()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Candy Bar", null, null, null, 20m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var addResponse = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));
        var cartAfterAdd = await addResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        var lineId = cartAfterAdd!.Lines.Single().Id;

        var removeResponse = await client.DeleteAsync($"/transactions/cart/lines/{lineId}");
        var afterRemove = await removeResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.Empty(afterRemove!.Lines);
        Assert.Equal(0m, afterRemove.TotalAmount);
    }

    [Fact]
    public async Task Adding_a_negative_quantity_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Chips", null, null, null, 25m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var response = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, -1m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Voiding_the_cart_then_getting_the_cart_starts_a_fresh_one()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var original = await client.GetFromJsonAsync<TransactionDto>("/transactions/cart", JsonOptions);

        var voidResponse = await client.PostAsync("/transactions/cart/void", null);
        var voided = await voidResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(TransactionStatus.Voided, voided!.Status);

        var fresh = await client.GetFromJsonAsync<TransactionDto>("/transactions/cart", JsonOptions);
        Assert.NotEqual(original!.Id, fresh!.Id);
    }

    [Fact]
    public async Task Paying_cash_with_enough_tendered_completes_the_sale_and_issues_a_receipt_number()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Soft Drink", null, null, null, 25m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 2m));

        var paymentResponse = await client.PostAsJsonAsync(
            "/transactions/cart/payments",
            new RecordPaymentRequest(PaymentMethod.Cash, 100m));

        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);
        var completed = await paymentResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(TransactionStatus.Completed, completed!.Status);
        Assert.Equal(1, completed.ReceiptNumber);
        var payment = Assert.Single(completed.Payments);
        Assert.Equal(50m, payment.Amount);
        Assert.Equal(50m, payment.ChangeGiven);

        var nextCart = await client.GetFromJsonAsync<TransactionDto>("/transactions/cart", JsonOptions);
        Assert.NotEqual(completed.Id, nextCart!.Id);
    }

    [Fact]
    public async Task Cash_tendered_less_than_the_total_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Energy Drink", null, null, null, 60m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));

        var paymentResponse = await client.PostAsJsonAsync(
            "/transactions/cart/payments",
            new RecordPaymentRequest(PaymentMethod.Cash, 10m));

        Assert.Equal(HttpStatusCode.BadRequest, paymentResponse.StatusCode);
    }

    [Fact]
    public async Task Paying_with_manual_gcash_qr_completes_the_sale_without_requiring_tendered_amount()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Notebook", null, null, null, 45m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));

        var paymentResponse = await client.PostAsJsonAsync(
            "/transactions/cart/payments",
            new RecordPaymentRequest(PaymentMethod.ManualGcashQr, null));

        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);
        var completed = await paymentResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(TransactionStatus.Completed, completed!.Status);
    }

    [Fact]
    public async Task Paying_via_qr_ph_is_rejected_as_not_yet_available()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Umbrella", null, null, null, 199m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));

        var paymentResponse = await client.PostAsJsonAsync(
            "/transactions/cart/payments",
            new RecordPaymentRequest(PaymentMethod.QrPh, null));

        Assert.Equal(HttpStatusCode.BadRequest, paymentResponse.StatusCode);
    }

    [Fact]
    public async Task Receipt_numbers_increment_sequentially_per_device_across_separate_sales()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Pen", null, null, null, 10m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item!.Id, null, 1m));
        var firstPayment = await client.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 10m));
        var firstCompleted = await firstPayment.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        var secondPayment = await client.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 10m));
        var secondCompleted = await secondPayment.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.Equal(1, firstCompleted!.ReceiptNumber);
        Assert.Equal(2, secondCompleted!.ReceiptNumber);
    }

    [Fact]
    public async Task Applying_the_senior_pwd_discount_takes_twenty_percent_off_the_total()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Rice Meal", null, null, null, 100m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));

        var discountResponse = await client.PutAsJsonAsync(
            "/transactions/cart/senior-pwd-discount",
            new ApplySeniorPwdDiscountRequest(true));

        Assert.Equal(HttpStatusCode.OK, discountResponse.StatusCode);
        var discounted = await discountResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.True(discounted!.SeniorPwdDiscountApplied);
        Assert.Equal(20m, discounted.DiscountAmount);
        Assert.Equal(80m, discounted.TotalAmount);
    }

    [Fact]
    public async Task The_discount_recalculates_when_a_line_is_added_after_it_is_applied()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Sandwich", null, null, null, 50m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));
        _ = await client.PutAsJsonAsync(
            "/transactions/cart/senior-pwd-discount",
            new ApplySeniorPwdDiscountRequest(true));

        var secondAddResponse = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item.Id, null, 1m));
        var cart = await secondAddResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.Equal(20m, cart!.DiscountAmount);
        Assert.Equal(80m, cart.TotalAmount);
    }

    [Fact]
    public async Task Turning_the_discount_back_off_restores_the_full_total()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Coffee", null, null, null, 40m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));
        _ = await client.PutAsJsonAsync(
            "/transactions/cart/senior-pwd-discount",
            new ApplySeniorPwdDiscountRequest(true));

        var offResponse = await client.PutAsJsonAsync(
            "/transactions/cart/senior-pwd-discount",
            new ApplySeniorPwdDiscountRequest(false));
        var cart = await offResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.False(cart!.SeniorPwdDiscountApplied);
        Assert.Equal(0m, cart.DiscountAmount);
        Assert.Equal(40m, cart.TotalAmount);
    }

    [Fact]
    public async Task Adding_a_variant_matrix_item_without_a_variant_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("T-Shirt", null, null, null, 200m, null, PricingType.VariantMatrix));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var response = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Adding_a_variant_matrix_item_with_a_variant_prices_off_the_override()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("T-Shirt", null, null, null, 200m, null, PricingType.VariantMatrix));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var variantResponse = await client.PostAsJsonAsync(
            $"/items/{item!.Id}/variants",
            new CreateItemVariantRequest(new Dictionary<string, string> { ["size"] = "L" }, null, 220m, null));
        var variant = await variantResponse.Content.ReadFromJsonAsync<ItemVariantDto>(JsonOptions);

        var addResponse = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item.Id, variant!.Id, 1m));
        var cart = await addResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        var line = Assert.Single(cart!.Lines);
        Assert.Equal(220m, line.UnitPrice);
        Assert.Equal(220m, cart.TotalAmount);
    }

    [Fact]
    public async Task Adding_a_combo_with_a_full_set_of_selections_prices_it_including_slot_upcharges()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var drinksResponse = await client.PostAsJsonAsync("/categories", new CreateCategoryRequest("Drinks", 1));
        var drinks = await drinksResponse.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);

        var sodaResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Soda", null, null, drinks!.Id, 25m, null, PricingType.Unit));
        var soda = await sodaResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var comboResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Value Meal", null, null, null, 150m, null, PricingType.Combo));
        var combo = await comboResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var slotResponse = await client.PostAsJsonAsync(
            $"/items/{combo!.Id}/combo-components",
            new CreateItemComboComponentRequest(drinks.Id, "Choose a Drink", 1, 10m));
        var slot = await slotResponse.Content.ReadFromJsonAsync<ItemComboComponentDto>(JsonOptions);

        var addResponse = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(
                combo.Id,
                null,
                1m,
                [new ComboSelectionRequest(slot!.Id, soda!.Id)]));
        var cart = await addResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        var line = Assert.Single(cart!.Lines);
        Assert.Equal(160m, line.UnitPrice);
        Assert.Equal(160m, cart.TotalAmount);
        var selection = Assert.Single(line.ComboSelections);
        Assert.Equal("Choose a Drink", selection.SlotLabel);
        Assert.Equal("Soda", selection.SelectedItemName);
    }

    private sealed record StaffLogin(HttpClient Client, Guid ManagerId, Guid CashierId);

    /// <summary>An admin who sets up a manager and a cashier, plus a client signed in as the cashier: the
    /// person whose login a queued sale ends up syncing under.</summary>
    private static async Task<(StaffLogin Staff, HttpClient Admin)> ManagerAndCashierAsync(PurchApiFactory factory)
    {
        var admin = await AuthenticatedAdminClientAsync(factory);
        _ = await admin.PostAsJsonAsync("/staff", new CreateStaffRequest("Mae Manager", Role.Manager, ScopeType.Tenant, null, null, "5678"));
        _ = await admin.PostAsJsonAsync("/staff", new CreateStaffRequest("Cal Cashier", Role.Cashier, ScopeType.Tenant, null, null, "6789"));
        var staff = await admin.GetFromJsonAsync<List<StaffDto>>("/staff", JsonOptions);
        var devices = await admin.GetFromJsonAsync<List<DeviceDto>>("/devices", JsonOptions);

        var cashier = factory.CreateClient();
        var login = await cashier.PostAsJsonAsync("/auth/login", new LoginRequest(devices!.Single().PairingCode, "6789"));
        var token = (await login.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions))!.AccessToken;
        cashier.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return (new StaffLogin(cashier, staff!.Single(x => x.Role == Role.Manager).Id, staff!.Single(x => x.Role == Role.Cashier).Id), admin);
    }

    private static CheckoutRequest OfflineSeniorSale(ItemDto item, Guid? rungBy)
    {
        return new(
            Guid.NewGuid(),
            [new AddTransactionLineRequest(item.Id, null, 1m)],
            SeniorPwdDiscountApplied: true,
            PromoCode: null,
            OrderType: null,
            Payment: new RecordPaymentRequest(PaymentMethod.Cash, 500m),
            ExpectedTotal: null,
            ReceiptNumber: null,
            OfflineSale: true,
            SoldAt: DateTimeOffset.UtcNow,
            RungByStaffId: rungBy);
    }

    [Fact]
    public async Task A_managers_senior_discount_sale_syncs_under_a_cashier_login_and_is_credited_to_the_manager()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var (staff, admin) = await ManagerAndCashierAsync(factory);
        using var _admin = admin;
        using var _cashier = staff.Client;
        var item = (await (await admin.PostAsJsonAsync("/items", new CreateItemRequest("Medicine", null, null, null, 100m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        var response = await staff.Client.PostAsJsonAsync("/transactions/checkout", OfflineSeniorSale(item, staff.ManagerId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        var report = await admin.GetFromJsonAsync<StaffPerformanceReportDto>($"/reports/staff-performance?from={from}&to={to}", JsonOptions);
        Assert.Equal(staff.ManagerId, Assert.Single(report!.Sales).StaffUserId);
    }

    [Fact]
    public async Task An_offline_senior_discount_sale_rung_by_a_cashier_or_an_unknown_person_is_still_refused()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var (staff, admin) = await ManagerAndCashierAsync(factory);
        using var _admin = admin;
        using var _cashier = staff.Client;
        var item = (await (await admin.PostAsJsonAsync("/items", new CreateItemRequest("Medicine", null, null, null, 100m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        // A cashier can't have applied it, and a claim is worthless if the person isn't in this tenant.
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.Client.PostAsJsonAsync("/transactions/checkout", OfflineSeniorSale(item, staff.CashierId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.Client.PostAsJsonAsync("/transactions/checkout", OfflineSeniorSale(item, Guid.NewGuid()))).StatusCode);
        // Without naming anyone, the old rule applies: the signed-in cashier isn't a supervisor.
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.Client.PostAsJsonAsync("/transactions/checkout", OfflineSeniorSale(item, null))).StatusCode);
    }

    [Fact]
    public async Task Selling_a_combo_takes_the_chosen_component_items_off_stock_not_the_combo_itself()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var branches = await client.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions);

        var drinks = (await (await client.PostAsJsonAsync("/categories", new CreateCategoryRequest("Drinks", 1))).Content.ReadFromJsonAsync<CategoryDto>(JsonOptions))!;
        var soda = (await (await client.PostAsJsonAsync("/items", new CreateItemRequest("Soda", null, null, drinks.Id, 25m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        var combo = (await (await client.PostAsJsonAsync("/items", new CreateItemRequest("Value Meal", null, null, null, 150m, null, PricingType.Combo))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        var slot = (await (await client.PostAsJsonAsync($"/items/{combo.Id}/combo-components", new CreateItemComboComponentRequest(drinks.Id, "Choose a Drink", 1, null))).Content.ReadFromJsonAsync<ItemComboComponentDto>(JsonOptions))!;
        var haircut = (await (await client.PostAsJsonAsync("/items", new CreateItemRequest("Haircut", null, null, null, 200m, null, PricingType.Service))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        _ = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(soda.Id, branches!.Single().Id, MovementType.StockIn, 10m, null, null, null, null));

        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(combo.Id, null, 2m, [new ComboSelectionRequest(slot.Id, soda.Id)]));
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(haircut.Id, null, 1m));
        var payment = await client.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 1000m));
        Assert.Equal(HttpStatusCode.OK, payment.StatusCode);

        var items = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        Assert.Equal(8m, items!.Single(i => i.Id == soda.Id).StockOnHand);
        Assert.Equal(0m, items!.Single(i => i.Id == combo.Id).StockOnHand);
        Assert.Equal(0m, items!.Single(i => i.Id == haircut.Id).StockOnHand);
    }

    [Fact]
    public async Task Adding_a_combo_missing_a_slot_selection_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var drinksResponse = await client.PostAsJsonAsync("/categories", new CreateCategoryRequest("Drinks", 1));
        var drinks = await drinksResponse.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);

        var comboResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Value Meal", null, null, null, 150m, null, PricingType.Combo));
        var combo = await comboResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            $"/items/{combo!.Id}/combo-components",
            new CreateItemComboComponentRequest(drinks!.Id, "Choose a Drink", 1, null));

        var response = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(combo.Id, null, 1m, []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Adding_a_combo_with_a_selection_from_the_wrong_category_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var drinksResponse = await client.PostAsJsonAsync("/categories", new CreateCategoryRequest("Drinks", 1));
        var drinks = await drinksResponse.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);

        var snacksResponse = await client.PostAsJsonAsync("/categories", new CreateCategoryRequest("Snacks", 2));
        var snacks = await snacksResponse.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);

        var chipsResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Chips", null, null, snacks!.Id, 30m, null, PricingType.Unit));
        var chips = await chipsResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var comboResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Value Meal", null, null, null, 150m, null, PricingType.Combo));
        var combo = await comboResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var slotResponse = await client.PostAsJsonAsync(
            $"/items/{combo!.Id}/combo-components",
            new CreateItemComboComponentRequest(drinks!.Id, "Choose a Drink", 1, null));
        var slot = await slotResponse.Content.ReadFromJsonAsync<ItemComboComponentDto>(JsonOptions);

        var response = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(
                combo.Id,
                null,
                1m,
                [new ComboSelectionRequest(slot!.Id, chips!.Id)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Applying_a_percentage_promo_code_discounts_the_subtotal()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        _ = await client.PostAsJsonAsync(
            "/promo-codes",
            new CreatePromoCodeRequest("SAVE10", PromoDiscountType.Percentage, 10m, null));

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Notebook", null, null, null, 100m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));

        var response = await client.PutAsJsonAsync("/transactions/cart/promo-code", new ApplyPromoCodeRequest("save10"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cart = await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal("SAVE10", cart!.PromoCode);
        Assert.Equal(10m, cart.PromoDiscountAmount);
        Assert.Equal(10m, cart.DiscountAmount);
        Assert.Equal(90m, cart.TotalAmount);
    }

    [Fact]
    public async Task Applying_a_fixed_amount_promo_code_is_capped_at_the_subtotal()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        _ = await client.PostAsJsonAsync(
            "/promo-codes",
            new CreatePromoCodeRequest("BIG50", PromoDiscountType.FixedAmount, 50m, null));

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Pencil", null, null, null, 20m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));

        var response = await client.PutAsJsonAsync("/transactions/cart/promo-code", new ApplyPromoCodeRequest("BIG50"));
        var cart = await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.Equal(20m, cart!.PromoDiscountAmount);
        Assert.Equal(0m, cart.TotalAmount);
    }

    [Fact]
    public async Task Applying_an_unknown_promo_code_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var response = await client.PutAsJsonAsync("/transactions/cart/promo-code", new ApplyPromoCodeRequest("NOPE"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Clearing_a_promo_code_restores_the_full_total()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        _ = await client.PostAsJsonAsync(
            "/promo-codes",
            new CreatePromoCodeRequest("SAVE10", PromoDiscountType.Percentage, 10m, null));

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Notebook", null, null, null, 100m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));
        _ = await client.PutAsJsonAsync("/transactions/cart/promo-code", new ApplyPromoCodeRequest("SAVE10"));

        var response = await client.PutAsJsonAsync("/transactions/cart/promo-code", new ApplyPromoCodeRequest(null));
        var cart = await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.Null(cart!.PromoCode);
        Assert.Equal(0m, cart.PromoDiscountAmount);
        Assert.Equal(100m, cart.TotalAmount);
    }

    // --- Discounts do not stack (RA 9994): Senior/PWD and promotions are either/or, and only one
    // promotion (item promos OR a promo code) applies at a time. The cashier chooses via the Senior/PWD switch. ---

    private static async Task<TransactionDto> CartWithOneAsync(HttpClient client, string name, decimal price)
    {
        var item = await CreateItemAsync(client, name, price);
        var response = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item.Id, null, 1m));
        return (await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
    }

    private static Task<HttpResponseMessage> CreatePromoCodeAsync(HttpClient client, string code, PromoDiscountType type, decimal value)
    {
        return client.PostAsJsonAsync("/promo-codes", new CreatePromoCodeRequest(code, type, value, null));
    }

    private static async Task<TransactionDto> ReadCartAsync(HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
    }

    [Fact]
    public async Task A_promo_code_does_not_stack_with_the_senior_pwd_discount()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        _ = await CreatePromoCodeAsync(client, "SAVE10", PromoDiscountType.Percentage, 10m);
        _ = await CartWithOneAsync(client, "Notebook", 100m);
        _ = await client.PutAsJsonAsync("/transactions/cart/senior-pwd-discount", new ApplySeniorPwdDiscountRequest(true));

        var cart = await ReadCartAsync(await client.PutAsJsonAsync("/transactions/cart/promo-code", new ApplyPromoCodeRequest("SAVE10")));

        // Only the 20% Senior/PWD discount applies; the code is kept on the cart but gives nothing.
        Assert.Equal("SAVE10", cart.PromoCode);
        Assert.Equal(0m, cart.PromoDiscountAmount);
        Assert.Equal(20m, cart.DiscountAmount);
        Assert.Equal(80m, cart.TotalAmount);
    }

    [Fact]
    public async Task Turning_the_senior_pwd_discount_off_restores_a_promo_code_it_had_suppressed()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        _ = await CreatePromoCodeAsync(client, "SAVE10", PromoDiscountType.Percentage, 10m);
        _ = await CartWithOneAsync(client, "Notebook", 100m);
        _ = await client.PutAsJsonAsync("/transactions/cart/promo-code", new ApplyPromoCodeRequest("SAVE10"));
        _ = await client.PutAsJsonAsync("/transactions/cart/senior-pwd-discount", new ApplySeniorPwdDiscountRequest(true));

        var off = await ReadCartAsync(await client.PutAsJsonAsync("/transactions/cart/senior-pwd-discount", new ApplySeniorPwdDiscountRequest(false)));

        Assert.Equal(10m, off.PromoDiscountAmount);
        Assert.Equal(90m, off.TotalAmount);
    }

    [Fact]
    public async Task The_senior_pwd_discount_is_taken_on_the_regular_price_and_suppresses_item_promos()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Promo Item", 100m);
        _ = await client.PostAsJsonAsync(
            "/promos/item-discounts",
            new CreateItemDiscountPromoRuleRequest("Half off", item.Id, PromoDiscountType.Percentage, 50m, null, null));
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));

        var withPromo = await ReadCartAsync(await client.GetAsync("/transactions/cart"));
        Assert.Equal(50m, withPromo.ItemPromoDiscountAmount);
        Assert.Equal(50m, withPromo.TotalAmount);

        var senior = await ReadCartAsync(await client.PutAsJsonAsync("/transactions/cart/senior-pwd-discount", new ApplySeniorPwdDiscountRequest(true)));

        // 20% of the REGULAR 100, not of the promo price; the promo is suppressed entirely.
        Assert.Equal(0m, senior.ItemPromoDiscountAmount);
        Assert.Equal(20m, senior.DiscountAmount);
        Assert.Equal(80m, senior.TotalAmount);
        Assert.Equal(0m, Assert.Single(senior.Lines).PromoDiscountAmount);
        Assert.Null(senior.Lines[0].AppliedPromoLabel);
    }

    [Fact]
    public async Task A_promo_code_and_item_promos_do_not_stack_and_the_larger_one_applies()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Stack Item", 100m);
        _ = await client.PostAsJsonAsync(
            "/promos/item-discounts",
            new CreateItemDiscountPromoRuleRequest("Ten off", item.Id, PromoDiscountType.FixedAmount, 10m, null, null));
        _ = await CreatePromoCodeAsync(client, "CODE30", PromoDiscountType.FixedAmount, 30m);
        _ = await CreatePromoCodeAsync(client, "CODE5", PromoDiscountType.FixedAmount, 5m);
        _ = await CreatePromoCodeAsync(client, "CODE10", PromoDiscountType.FixedAmount, 10m);
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));

        // The code (30) beats the item promo (10): only the code applies.
        var codeWins = await ReadCartAsync(await client.PutAsJsonAsync("/transactions/cart/promo-code", new ApplyPromoCodeRequest("CODE30")));
        Assert.Equal(30m, codeWins.PromoDiscountAmount);
        Assert.Equal(0m, codeWins.ItemPromoDiscountAmount);
        Assert.Equal(70m, codeWins.TotalAmount);

        // A second code REPLACES the first (one promo code per order), and here loses to the item promo (5 < 10).
        var promosWin = await ReadCartAsync(await client.PutAsJsonAsync("/transactions/cart/promo-code", new ApplyPromoCodeRequest("CODE5")));
        Assert.Equal("CODE5", promosWin.PromoCode);
        Assert.Equal(0m, promosWin.PromoDiscountAmount);
        Assert.Equal(10m, promosWin.ItemPromoDiscountAmount);
        Assert.Equal(90m, promosWin.TotalAmount);

        // A tie goes to the automatic item promo.
        var tie = await ReadCartAsync(await client.PutAsJsonAsync("/transactions/cart/promo-code", new ApplyPromoCodeRequest("CODE10")));
        Assert.Equal(10m, tie.ItemPromoDiscountAmount);
        Assert.Equal(0m, tie.PromoDiscountAmount);
        Assert.Equal(90m, tie.TotalAmount);
    }

    [Fact]
    public async Task Checkout_prices_the_senior_pwd_discount_and_a_promo_code_exclusively()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Checkout Notebook", 100m);
        _ = await CreatePromoCodeAsync(client, "SAVE10", PromoDiscountType.Percentage, 10m);

        var response = await client.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(
                Guid.NewGuid(),
                [new AddTransactionLineRequest(item.Id, null, 1m)],
                SeniorPwdDiscountApplied: true,
                PromoCode: "SAVE10",
                OrderType: null,
                Payment: new RecordPaymentRequest(PaymentMethod.Cash, 100m),
                // What the device shows: Senior/PWD only. Stacking would make it 70 and trip the 409.
                ExpectedTotal: 80m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sale = await ReadCartAsync(response);
        Assert.Equal(80m, sale.TotalAmount);
        Assert.Equal(0m, sale.PromoDiscountAmount);
        Assert.Equal(20m, sale.DiscountAmount);
    }

    [Fact]
    public async Task Checkout_builds_prices_and_pays_a_whole_sale_in_one_call()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Checkout Water", 15m);

        var response = await client.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(
                Guid.NewGuid(),
                [new AddTransactionLineRequest(item.Id, null, 2m)],
                SeniorPwdDiscountApplied: false,
                PromoCode: null,
                OrderType: "Take Out",
                Payment: new RecordPaymentRequest(PaymentMethod.Cash, 50m),
                ExpectedTotal: 30m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sale = await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(TransactionStatus.Completed, sale!.Status);
        Assert.Equal(30m, sale.TotalAmount);
        Assert.Equal("Take Out", sale.OrderType);
        _ = Assert.NotNull(sale.ReceiptNumber);
        Assert.Equal(20m, Assert.Single(sale.Payments).ChangeGiven);
    }

    [Fact]
    public async Task Checkout_retried_with_the_same_sale_id_returns_the_same_sale_and_does_not_charge_twice()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Retry Water", 10m);
        var request = new CheckoutRequest(
            Guid.NewGuid(),
            [new AddTransactionLineRequest(item.Id, null, 1m)],
            false,
            null,
            null,
            new RecordPaymentRequest(PaymentMethod.Cash, 10m));

        var first = await (await client.PostAsJsonAsync("/transactions/checkout", request))
            .Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        var retry = await client.PostAsJsonAsync("/transactions/checkout", request);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var second = await retry.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(first!.Id, second!.Id);
        Assert.Equal(first.ReceiptNumber, second.ReceiptNumber);
        _ = Assert.Single(second.Payments);
    }

    [Fact]
    public async Task Checkout_stops_before_charging_when_the_server_prices_the_cart_differently()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Price Check Water", 10m);

        var response = await client.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(
                Guid.NewGuid(),
                [new AddTransactionLineRequest(item.Id, null, 1m)],
                false,
                null,
                null,
                new RecordPaymentRequest(PaymentMethod.Cash, 100m),
                ExpectedTotal: 5m));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_with_an_empty_cart_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(
                Guid.NewGuid(),
                [],
                false,
                null,
                null,
                new RecordPaymentRequest(PaymentMethod.Cash, 10m)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_replaces_a_leftover_open_cart_instead_of_adding_to_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var leftover = await CreateItemAsync(client, "Leftover", 99m);
        var item = await CreateItemAsync(client, "Fresh Sale Item", 10m);
        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(leftover.Id, null, 1m));

        var response = await client.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(
                Guid.NewGuid(),
                [new AddTransactionLineRequest(item.Id, null, 1m)],
                false,
                null,
                null,
                new RecordPaymentRequest(PaymentMethod.Cash, 10m)));

        var sale = await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(10m, sale!.TotalAmount);
        Assert.Equal(item.Id, Assert.Single(sale.Lines).ItemId);
    }

    [Fact]
    public async Task Checkout_honours_a_device_issued_receipt_number_and_moves_the_sequence_up_to_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Numbered Water", 10m);

        var response = await client.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(
                Guid.NewGuid(),
                [new AddTransactionLineRequest(item.Id, null, 1m)],
                false,
                null,
                null,
                new RecordPaymentRequest(PaymentMethod.Cash, 10m),
                ReceiptNumber: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sale = await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(7, sale!.ReceiptNumber);

        var sequence = await client.GetFromJsonAsync<JsonElement>("/transactions/receipt-sequence", JsonOptions);
        Assert.Equal(7, sequence.GetProperty("lastIssuedNumber").GetInt64());
    }

    [Fact]
    public async Task Checkout_rejects_a_receipt_number_this_terminal_already_used()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Duplicate Number Water", 10m);

        CheckoutRequest Sale(long number)
        {
            return new(
            Guid.NewGuid(),
            [new AddTransactionLineRequest(item.Id, null, 1m)],
            false,
            null,
            null,
            new RecordPaymentRequest(PaymentMethod.Cash, 10m),
            ReceiptNumber: number);
        }

        var first = await client.PostAsJsonAsync("/transactions/checkout", Sale(3));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var duplicate = await client.PostAsJsonAsync("/transactions/checkout", Sale(3));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Checkout_rejects_an_out_of_range_receipt_number()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Range Water", 10m);

        var response = await client.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(
                Guid.NewGuid(),
                [new AddTransactionLineRequest(item.Id, null, 1m)],
                false,
                null,
                null,
                new RecordPaymentRequest(PaymentMethod.Cash, 10m),
                ReceiptNumber: 1_000_000));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_records_an_offline_sale_even_when_the_server_prices_it_differently()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Offline Water", 10m);

        var response = await client.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(
                Guid.NewGuid(),
                [new AddTransactionLineRequest(item.Id, null, 1m)],
                false,
                null,
                null,
                new RecordPaymentRequest(PaymentMethod.Cash, 100m),
                ExpectedTotal: 5m,
                ReceiptNumber: 11,
                OfflineSale: true,
                SoldAt: DateTimeOffset.UtcNow.AddHours(-3)));

        // The same request as an online sale is refused with a 409 (see the price-check test above);
        // offline, the customer already paid, so the sale is recorded at the server's price.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sale = await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(TransactionStatus.Completed, sale!.Status);
        Assert.Equal(10m, sale.TotalAmount);
        Assert.Equal(11, sale.ReceiptNumber);
    }

    private static async Task<ItemDto> CreateItemAsync(HttpClient client, string name, decimal price)
    {
        var response = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest(name, null, null, null, price, null, PricingType.Unit));
        return (await response.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
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
