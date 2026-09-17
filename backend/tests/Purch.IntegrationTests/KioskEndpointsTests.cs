using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Kiosk;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
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
