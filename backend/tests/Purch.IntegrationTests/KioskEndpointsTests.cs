using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Devices;
using Purch.Application.Kiosk;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Application.Promotions;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class KioskEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Pairing_with_an_unknown_code_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/kiosk/session", new KioskSessionRequest("not-a-real-code", "1234"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Pairing_with_a_valid_code_returns_a_kiosk_scoped_token()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var (kioskClient, _, _) = await PairedKioskClientAsync(factory);

        var response = await kioskClient.GetAsync("/kiosk/cart");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_kiosk_token_cannot_reach_cashier_only_endpoints()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var (kioskClient, _, _) = await PairedKioskClientAsync(factory);

        var cartResponse = await kioskClient.GetAsync("/kiosk/cart");
        var cart = await cartResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        // Never payment, discount, or promo — those all live under posOperator-only routes.
        var paymentResponse = await kioskClient.PostAsJsonAsync(
            "/transactions/cart/payments",
            new RecordPaymentRequest(PaymentMethod.Cash, cart!.TotalAmount));
        var discountResponse = await kioskClient.PutAsJsonAsync(
            "/transactions/cart/senior-pwd-discount",
            new ApplySeniorPwdDiscountRequest(true));
        var promoResponse = await kioskClient.PutAsJsonAsync(
            "/transactions/cart/promo-code",
            new ApplyPromoCodeRequest("ANYCODE"));

        Assert.Equal(HttpStatusCode.Forbidden, paymentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, discountResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, promoResponse.StatusCode);
    }

    [Fact]
    public async Task Building_and_submitting_a_kiosk_order_issues_a_prep_number_and_flags_it_kiosk_originated()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var adminClient = await AuthenticatedAdminClientAsync(factory);
        var (kioskClient, _, _) = await PairedKioskClientAsync(factory, adminClient);

        var itemResponse = await adminClient.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Rice Meal", null, null, null, 85m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await kioskClient.PostAsJsonAsync("/kiosk/cart/lines", new AddTransactionLineRequest(item!.Id, null, 2m));
        _ = await kioskClient.PutAsJsonAsync("/kiosk/cart/order-type", new SetOrderTypeRequest("Take Out"));

        var submitResponse = await kioskClient.PostAsync("/kiosk/cart/submit", null);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        Assert.Equal(TransactionStatus.AwaitingPayment, submitted!.Status);
        Assert.True(submitted.OriginatedFromKiosk);
        _ = Assert.NotNull(submitted.KioskPrepNumber);
        Assert.Equal("Take Out", submitted.OrderType);
        Assert.Equal(170m, submitted.TotalAmount);

        // The kiosk's next customer gets a fresh cart — the submitted one is no longer "open".
        var nextCart = await kioskClient.GetFromJsonAsync<TransactionDto>("/kiosk/cart", JsonOptions);
        Assert.NotEqual(submitted.Id, nextCart!.Id);
    }

    [Fact]
    public async Task Placing_a_kiosk_order_builds_and_submits_it_in_one_call()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var adminClient = await AuthenticatedAdminClientAsync(factory);
        var (kioskClient, _, _) = await PairedKioskClientAsync(factory, adminClient);

        var itemResponse = await adminClient.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Rice Meal", null, null, null, 85m, null, PricingType.Unit));
        var item = (await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        var orderId = Guid.NewGuid();
        var response = await kioskClient.PostAsJsonAsync(
            "/kiosk/cart/place-order",
            new PlaceKioskOrderRequest(orderId, [new AddTransactionLineRequest(item.Id, null, 2m)], "Take Out"));
        var placed = await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TransactionStatus.AwaitingPayment, placed!.Status);
        Assert.True(placed.OriginatedFromKiosk);
        _ = Assert.NotNull(placed.KioskPrepNumber);
        Assert.Equal("Take Out", placed.OrderType);
        Assert.Equal(170m, placed.TotalAmount);

        // The kiosk's next customer gets a fresh cart.
        var nextCart = await kioskClient.GetFromJsonAsync<TransactionDto>("/kiosk/cart", JsonOptions);
        Assert.NotEqual(placed.Id, nextCart!.Id);
    }

    [Fact]
    public async Task Retrying_the_same_kiosk_order_id_returns_the_already_placed_order_instead_of_duplicating_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var adminClient = await AuthenticatedAdminClientAsync(factory);
        var (kioskClient, branchId, _) = await PairedKioskClientAsync(factory, adminClient);

        var item = (await (await adminClient.PostAsJsonAsync("/items", new CreateItemRequest("Siomai", null, null, null, 45m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        var orderId = Guid.NewGuid();
        var request = new PlaceKioskOrderRequest(orderId, [new AddTransactionLineRequest(item.Id, null, 1m)], "Take Out");
        var first = await (await kioskClient.PostAsJsonAsync("/kiosk/cart/place-order", request)).Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        var retry = await (await kioskClient.PostAsJsonAsync("/kiosk/cart/place-order", request)).Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.Equal(first!.Id, retry!.Id);
        Assert.Equal(first.KioskPrepNumber, retry.KioskPrepNumber);

        var pending = await adminClient.GetFromJsonAsync<List<TransactionDto>>($"/transactions/kiosk-pending?branchId={branchId}", JsonOptions);
        Assert.Single(pending!, order => order.Id == first.Id);
    }

    [Fact]
    public async Task Placing_a_kiosk_order_with_an_inactive_item_is_refused_and_a_retry_still_works()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var adminClient = await AuthenticatedAdminClientAsync(factory);
        var (kioskClient, _, _) = await PairedKioskClientAsync(factory, adminClient);

        var item = (await (await adminClient.PostAsJsonAsync("/items", new CreateItemRequest("Halo-Halo", null, null, null, 60m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        _ = await adminClient.PutAsJsonAsync($"/items/{item.Id}", new UpdateItemRequest(item.Name, item.Sku, item.Barcode, item.CategoryId, item.BasePrice, item.ImageUrl, false, item.DepartmentId));

        var orderId = Guid.NewGuid();
        var request = new PlaceKioskOrderRequest(orderId, [new AddTransactionLineRequest(item.Id, null, 1m)], "Take Out");
        var refused = await kioskClient.PostAsJsonAsync("/kiosk/cart/place-order", request);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        // The customer is told which item, not just that something failed.
        Assert.Contains("Halo-Halo is no longer available.", await refused.Content.ReadAsStringAsync());

        // Nothing was queued for the kitchen.
        var cart = await kioskClient.GetFromJsonAsync<TransactionDto>("/kiosk/cart", JsonOptions);
        Assert.Empty(cart!.Lines);

        // A retry under the same order id, once the order is fixed client-side, still works.
        _ = await adminClient.PutAsJsonAsync($"/items/{item.Id}", new UpdateItemRequest(item.Name, item.Sku, item.Barcode, item.CategoryId, item.BasePrice, item.ImageUrl, true, item.DepartmentId));
        var retried = await kioskClient.PostAsJsonAsync("/kiosk/cart/place-order", request);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
    }

    [Fact]
    public async Task The_kiosk_can_read_active_item_promotions_but_the_server_still_prices_the_order()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var adminClient = await AuthenticatedAdminClientAsync(factory);
        var (kioskClient, _, _) = await PairedKioskClientAsync(factory, adminClient);

        var item = (await (await adminClient.PostAsJsonAsync("/items", new CreateItemRequest("Halo-Halo", null, null, null, 100m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        var rule = await adminClient.PostAsJsonAsync("/promos/item-discounts", new CreateItemDiscountPromoRuleRequest("Merienda", item.Id, PromoDiscountType.Percentage, 10m, null, null));
        Assert.Equal(HttpStatusCode.OK, rule.StatusCode);

        var rules = await kioskClient.GetFromJsonAsync<KioskPromoRules>("/kiosk/promo-rules", JsonOptions);
        var discount = Assert.Single(rules!.ItemDiscounts);
        Assert.Equal(item.Id, discount.ItemId);

        var placed = await (await kioskClient.PostAsJsonAsync(
            "/kiosk/cart/place-order",
            new PlaceKioskOrderRequest(Guid.NewGuid(), [new AddTransactionLineRequest(item.Id, null, 1m)], "Take Out"))).Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(90m, placed!.TotalAmount);
    }

    private sealed record KioskPromoRules(
        IReadOnlyList<BogoPromoRuleDto> Bogo,
        IReadOnlyList<ComboPromoRuleDto> Combos,
        IReadOnlyList<ItemDiscountPromoRuleDto> ItemDiscounts);

    [Fact]
    public async Task A_cashier_can_claim_a_submitted_kiosk_order_and_complete_payment_on_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var adminClient = await AuthenticatedAdminClientAsync(factory);
        var (kioskClient, branchId, _) = await PairedKioskClientAsync(factory, adminClient);

        var itemResponse = await adminClient.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Iced Tea", null, null, null, 40m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await kioskClient.PostAsJsonAsync("/kiosk/cart/lines", new AddTransactionLineRequest(item!.Id, null, 1m));
        var submitResponse = await kioskClient.PostAsync("/kiosk/cart/submit", null);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        var pendingResponse = await adminClient.GetAsync($"/transactions/kiosk-pending?branchId={branchId}");
        var pending = await pendingResponse.Content.ReadFromJsonAsync<List<TransactionDto>>(JsonOptions);
        Assert.Contains(pending!, order => order.Id == submitted!.Id);

        var claimResponse = await adminClient.PostAsync($"/transactions/kiosk-pending/{submitted!.Id}/claim", null);
        var claimed = await claimResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(HttpStatusCode.OK, claimResponse.StatusCode);
        Assert.Equal(TransactionStatus.Open, claimed!.Status);

        // Now it's just the admin's own open cart — the exact same payment pipeline as any other sale.
        var paymentResponse = await adminClient.PostAsJsonAsync(
            "/transactions/cart/payments",
            new RecordPaymentRequest(PaymentMethod.Cash, 40m));
        var paid = await paymentResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);
        Assert.Equal(TransactionStatus.Completed, paid!.Status);
        _ = Assert.NotNull(paid.ReceiptNumber);

        var stillPendingResponse = await adminClient.GetAsync($"/transactions/kiosk-pending?branchId={branchId}");
        var stillPending = await stillPendingResponse.Content.ReadFromJsonAsync<List<TransactionDto>>(JsonOptions);
        Assert.DoesNotContain(stillPending!, order => order.Id == submitted.Id);
    }

    /// <summary>Builds, submits and claims a one-line kiosk order, leaving it Open with KitchenStatus.Queued
    /// under whichever client claims it — the shared setup for the kitchen-edit gate tests below.</summary>
    private static async Task<(TransactionDto Claimed, ItemDto Item)> ClaimedKioskOrderAsync(PurchApiFactory factory, HttpClient adminClient, HttpClient claimingClient)
    {
        var (kioskClient, _, _) = await PairedKioskClientAsync(factory, adminClient);
        var itemResponse = await adminClient.PostAsJsonAsync("/items", new CreateItemRequest("Ramen", null, null, null, 150m, null, PricingType.Unit));
        var item = (await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        _ = await kioskClient.PostAsJsonAsync("/kiosk/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        var submitResponse = await kioskClient.PostAsync("/kiosk/cart/submit", null);
        var submitted = (await submitResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;

        var claimResponse = await claimingClient.PostAsync($"/transactions/kiosk-pending/{submitted.Id}/claim", null);
        var claimed = (await claimResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
        return (claimed, item);
    }

    [Fact]
    public async Task A_cashier_editing_a_not_yet_prepared_kitchen_order_needs_a_managers_pin()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var adminClient = await AuthenticatedAdminClientAsync(factory);
        _ = await adminClient.PostAsJsonAsync("/staff", new CreateStaffRequest("Mae Manager", Role.Manager, ScopeType.Tenant, null, null, "5678"));
        using var cashierClient = await CashierClientAsync(adminClient, factory);
        var (claimed, item) = await ClaimedKioskOrderAsync(factory, adminClient, cashierClient);
        var lineId = claimed.Lines.Single().Id;

        var noPin = await cashierClient.PutAsJsonAsync($"/transactions/cart/lines/{lineId}", new UpdateTransactionLineRequest(2m));
        Assert.Equal(HttpStatusCode.BadRequest, noPin.StatusCode);
        Assert.Equal(1m, (await cashierClient.GetFromJsonAsync<TransactionDto>("/transactions/cart", JsonOptions))!.Lines.Single().Quantity);

        var wrongPin = await cashierClient.PutAsJsonAsync($"/transactions/cart/lines/{lineId}", new UpdateTransactionLineRequest(2m, "0000"));
        Assert.Equal(HttpStatusCode.BadRequest, wrongPin.StatusCode);

        var withManagerPin = await cashierClient.PutAsJsonAsync($"/transactions/cart/lines/{lineId}", new UpdateTransactionLineRequest(2m, "5678"));
        Assert.Equal(HttpStatusCode.OK, withManagerPin.StatusCode);
        var updated = await withManagerPin.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(2m, updated!.Lines.Single().Quantity);
        _ = item;

        var manager = (await adminClient.GetFromJsonAsync<List<StaffDto>>("/staff", JsonOptions))!.Single(s => s.Name == "Mae Manager");
        var auditLogs = await adminClient.GetFromJsonAsync<List<AuditLogDto>>("/audit-logs?actionType=KitchenOrderLineEdited", JsonOptions);
        var entry = Assert.Single(auditLogs!, log => log.TargetEntityId == lineId);
        Assert.Equal(manager.Id, entry.ApprovedByUserId);
    }

    [Fact]
    public async Task An_admin_editing_a_not_yet_prepared_kitchen_order_needs_no_pin()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var adminClient = await AuthenticatedAdminClientAsync(factory);
        var (claimed, _) = await ClaimedKioskOrderAsync(factory, adminClient, adminClient);
        var lineId = claimed.Lines.Single().Id;

        var response = await adminClient.PutAsJsonAsync($"/transactions/cart/lines/{lineId}", new UpdateTransactionLineRequest(3m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3m, (await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!.Lines.Single().Quantity);

        // The admin edited their own kiosk order — no approval happened, so nothing unusual to log.
        var auditLogs = await adminClient.GetFromJsonAsync<List<AuditLogDto>>("/audit-logs?actionType=KitchenOrderLineEdited", JsonOptions);
        Assert.Empty(auditLogs!);
    }

    [Fact]
    public async Task Removing_a_line_from_a_not_yet_prepared_kitchen_order_follows_the_same_pin_rule()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var adminClient = await AuthenticatedAdminClientAsync(factory);
        _ = await adminClient.PostAsJsonAsync("/staff", new CreateStaffRequest("Mae Manager", Role.Manager, ScopeType.Tenant, null, null, "5678"));
        using var cashierClient = await CashierClientAsync(adminClient, factory);
        var (claimed, _) = await ClaimedKioskOrderAsync(factory, adminClient, cashierClient);
        var lineId = claimed.Lines.Single().Id;

        var noPin = await cashierClient.DeleteAsync($"/transactions/cart/lines/{lineId}");
        Assert.Equal(HttpStatusCode.BadRequest, noPin.StatusCode);

        var withPin = await cashierClient.DeleteAsync($"/transactions/cart/lines/{lineId}?approverPin=5678");
        Assert.Equal(HttpStatusCode.OK, withPin.StatusCode);
        Assert.Empty((await withPin.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!.Lines);

        var manager = (await adminClient.GetFromJsonAsync<List<StaffDto>>("/staff", JsonOptions))!.Single(s => s.Name == "Mae Manager");
        var auditLogs = await adminClient.GetFromJsonAsync<List<AuditLogDto>>("/audit-logs?actionType=KitchenOrderLineEdited", JsonOptions);
        var entry = Assert.Single(auditLogs!, log => log.TargetEntityId == lineId);
        Assert.Equal(manager.Id, entry.ApprovedByUserId);
    }

    [Fact]
    public async Task Once_the_kitchen_has_started_preparing_it_no_pin_can_change_the_order()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var adminClient = await AuthenticatedAdminClientAsync(factory);
        var (claimed, _) = await ClaimedKioskOrderAsync(factory, adminClient, adminClient);
        var lineId = claimed.Lines.Single().Id;

        var (kitchenClient, _, _) = await PairedKitchenDisplayClientAsync(factory, adminClient);
        var statusResponse = await kitchenClient.PutAsJsonAsync($"/kitchen-display/orders/{claimed.Id}/status", new UpdateKitchenStatusRequest(KitchenStatus.Preparing));
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        // Even the admin who needed no PIN a moment ago cannot edit it now — a PIN is not the point once
        // the kitchen has actually started.
        var response = await adminClient.PutAsJsonAsync($"/transactions/cart/lines/{lineId}", new UpdateTransactionLineRequest(5m));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1m, (await adminClient.GetFromJsonAsync<TransactionDto>("/transactions/cart", JsonOptions))!.Lines.Single().Quantity);
    }

    [Fact]
    public async Task Editing_an_ordinary_non_kiosk_cart_never_needs_a_pin()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var adminClient = await AuthenticatedAdminClientAsync(factory);
        _ = await adminClient.PostAsJsonAsync("/staff", new CreateStaffRequest("Mae Manager", Role.Manager, ScopeType.Tenant, null, null, "5678"));
        using var cashierClient = await CashierClientAsync(adminClient, factory);
        var item = (await (await adminClient.PostAsJsonAsync("/items", new CreateItemRequest("Soda", null, null, null, 30m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        var addResponse = await cashierClient.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        var lineId = (await addResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!.Lines.Single().Id;

        var response = await cashierClient.PutAsJsonAsync($"/transactions/cart/lines/{lineId}", new UpdateTransactionLineRequest(4m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4m, (await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!.Lines.Single().Quantity);
    }

    private static async Task<(HttpClient Client, Guid BranchId, Guid DeviceId)> PairedKitchenDisplayClientAsync(PurchApiFactory factory, HttpClient adminClient)
    {
        var branches = await adminClient.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions);
        var branchId = branches!.Single().Id;
        var deviceResponse = await adminClient.PostAsJsonAsync("/devices", new CreateDeviceRequest(branchId, DeviceIdentifier: null, DeviceType.KitchenDisplay, PairingPin: "9999"));
        var device = (await deviceResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions))!;

        var client = factory.CreateClient();
        var session = await client.PostAsJsonAsync("/kitchen-display/session", new UnattendedSessionRequest(device.PairingCode, "9999"));
        var token = (await session.Content.ReadFromJsonAsync<KioskSessionResponseBody>(JsonOptions))!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, branchId, device.Id);
    }

    [Fact]
    public async Task Claiming_a_kiosk_order_is_rejected_if_the_cashier_already_has_an_open_cart()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var adminClient = await AuthenticatedAdminClientAsync(factory);
        var (kioskClient, _, _) = await PairedKioskClientAsync(factory, adminClient);

        var itemResponse = await adminClient.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Chips", null, null, null, 25m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        // Admin already has their own open cart running.
        _ = await adminClient.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item!.Id, null, 1m));

        _ = await kioskClient.PostAsJsonAsync("/kiosk/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        var submitResponse = await kioskClient.PostAsync("/kiosk/cart/submit", null);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        var claimResponse = await adminClient.PostAsync($"/transactions/kiosk-pending/{submitted!.Id}/claim", null);

        Assert.Equal(HttpStatusCode.BadRequest, claimResponse.StatusCode);
    }

    [Fact]
    public async Task Resetting_a_devices_pairing_code_invalidates_the_old_code_and_revokes_its_sessions()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var adminClient = await AuthenticatedAdminClientAsync(factory);
        var (kioskClient, _, deviceId) = await PairedKioskClientAsync(factory, adminClient);

        var devicesBeforeReset = await adminClient.GetFromJsonAsync<List<DeviceDto>>("/devices", JsonOptions);
        var oldPairingCode = devicesBeforeReset!.Single(d => d.Id == deviceId).PairingCode;

        var resetResponse = await adminClient.PostAsync($"/devices/{deviceId}/reset-pairing-code", null);
        var resetDevice = await resetResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        Assert.NotNull(resetDevice);
        Assert.NotEqual(oldPairingCode, resetDevice!.PairingCode);

        // The token issued under the old pairing code no longer works...
        var cartAfterResetResponse = await kioskClient.GetAsync("/kiosk/cart");
        Assert.Equal(HttpStatusCode.Unauthorized, cartAfterResetResponse.StatusCode);

        // ...pairing with the old code is rejected...
        using var staleKioskClient = factory.CreateClient();
        var staleSessionResponse = await staleKioskClient.PostAsJsonAsync("/kiosk/session", new KioskSessionRequest(oldPairingCode, "5678"));
        Assert.Equal(HttpStatusCode.Unauthorized, staleSessionResponse.StatusCode);

        // ...but pairing with the new code succeeds.
        using var freshKioskClient = factory.CreateClient();
        var freshSessionResponse = await freshKioskClient.PostAsJsonAsync(
            "/kiosk/session",
            new KioskSessionRequest(resetDevice.PairingCode, "5678"));
        Assert.Equal(HttpStatusCode.OK, freshSessionResponse.StatusCode);
    }

    [Fact]
    public async Task Resetting_a_devices_pairing_pin_invalidates_the_old_pin_and_revokes_its_sessions()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var adminClient = await AuthenticatedAdminClientAsync(factory);
        var (kioskClient, _, deviceId) = await PairedKioskClientAsync(factory, adminClient);

        var resetResponse = await adminClient.PostAsJsonAsync(
            $"/devices/{deviceId}/reset-pairing-pin",
            new ResetDevicePairingPinRequest("4321"));
        var resetDevice = await resetResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        // The old session is revoked by the PIN reset...
        var cartAfterResetResponse = await kioskClient.GetAsync("/kiosk/cart");
        Assert.Equal(HttpStatusCode.Unauthorized, cartAfterResetResponse.StatusCode);

        // ...the old PIN no longer works...
        using var oldPinClient = factory.CreateClient();
        var oldPinResponse = await oldPinClient.PostAsJsonAsync("/kiosk/session", new KioskSessionRequest(resetDevice!.PairingCode, "5678"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldPinResponse.StatusCode);

        // ...but the new one does.
        using var newPinClient = factory.CreateClient();
        var newPinResponse = await newPinClient.PostAsJsonAsync("/kiosk/session", new KioskSessionRequest(resetDevice.PairingCode, "4321"));
        Assert.Equal(HttpStatusCode.OK, newPinResponse.StatusCode);
    }

    private static async Task<(HttpClient KioskClient, Guid BranchId, Guid DeviceId)> PairedKioskClientAsync(
        PurchApiFactory factory,
        HttpClient? existingAdminClient = null)
    {
        var adminClient = existingAdminClient ?? await AuthenticatedAdminClientAsync(factory);
        var branches = await adminClient.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions);
        var branchId = branches!.Single().Id;

        var deviceResponse = await adminClient.PostAsJsonAsync(
            "/devices",
            new CreateDeviceRequest(branchId, "Kiosk Terminal 1", DeviceType.Kiosk, "5678"));
        var device = await deviceResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);

        var kioskClient = factory.CreateClient();
        var sessionResponse = await kioskClient.PostAsJsonAsync("/kiosk/session", new KioskSessionRequest(device!.PairingCode, "5678"));
        var session = await sessionResponse.Content.ReadFromJsonAsync<KioskSessionResponseBody>(JsonOptions);

        kioskClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return (kioskClient, branchId, device.Id);
    }

    /// <summary>A cashier signed in on the same tenant/device as an already-paired admin — for the
    /// kitchen-edit gate, where who is asking (not just who is signed in on the same terminal) matters.</summary>
    private static async Task<HttpClient> CashierClientAsync(HttpClient adminClient, PurchApiFactory factory, string pin = "6789")
    {
        _ = await adminClient.PostAsJsonAsync("/staff", new CreateStaffRequest("Cal Cashier", Role.Cashier, ScopeType.Tenant, null, null, pin));
        var devices = await adminClient.GetFromJsonAsync<List<DeviceDto>>("/devices", JsonOptions);
        var registerDevice = devices!.Single(d => d.DeviceType == DeviceType.Register);

        var cashier = factory.CreateClient();
        var login = await cashier.PostAsJsonAsync("/auth/login", new LoginRequest(registerDevice.PairingCode, pin));
        var token = (await login.Content.ReadFromJsonAsync<KioskSessionResponseBody>(JsonOptions))!.AccessToken;
        cashier.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cashier;
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
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<KioskSessionResponseBody>(JsonOptions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);
        return client;
    }

    private sealed record KioskSessionResponseBody(string AccessToken);
}
