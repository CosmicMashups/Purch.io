using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Devices;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class OnboardingEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Bootstrap_then_sign_in_with_the_owner_email_and_password_succeeds_and_creates_no_device()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";

        var bootstrapResponse = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest("Ana's Sari-Sari", BusinessType.ConvenienceStore, "Main Branch", "Ana Reyes", "123412", email, "correct horse battery"));

        Assert.Equal(HttpStatusCode.OK, bootstrapResponse.StatusCode);
        var result = (await bootstrapResponse.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions))!;

        var signIn = await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, "correct horse battery"));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        using var owner = TestSessions.Bearer(factory, (await signIn.Content.ReadFromJsonAsync<SessionBody>(JsonOptions))!.AccessToken);
        Assert.Empty((await owner.GetFromJsonAsync<List<DeviceDto>>("/devices", JsonOptions))!);
        Assert.Equal(result.BranchId, Assert.Single((await owner.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions))!).Id);
    }

    [Theory]
    [InlineData(null, "correct horse battery")]
    [InlineData("ana@example.com", null)]
    [InlineData("ana@example.com", "short")]
    public async Task Bootstrap_needs_an_email_and_a_password_that_meets_the_rules(string? email, string? password)
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest("Ana's Sari-Sari", BusinessType.ConvenienceStore, "Main Branch", "Ana Reyes", "123412", email, password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_with_missing_fields_returns_400_with_field_errors()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest(string.Empty, BusinessType.ConvenienceStore, string.Empty, "Ana", "123412"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Read_access_to_staff_and_promo_codes_is_limited_to_the_roles_that_need_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var cashier = await TestSessions.CashierClientAsync(admin, "567812", "Cash Ier");
        using var warehouse = await TestSessions.StaffClientAsync(admin, "Ware House", MembershipRole.Staff, StaffDuty.Warehouse, "678912");

        // The staff list (names, roles, scopes) is for managers only.
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/staff/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync("/staff/members")).StatusCode);

        // The cashier's POS prices with promo data, so it keeps read access; warehouse has no use for it.
        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync("/promo-codes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync("/promos/bogo")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await warehouse.GetAsync("/promo-codes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await warehouse.GetAsync("/promos/bogo")).StatusCode);
    }

    [Fact]
    public async Task The_admin_pin_chosen_at_bootstrap_must_be_digits()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest($"Tenant-{Guid.NewGuid():N}", BusinessType.ConvenienceStore, "Main Branch", "Admin User", "abcd", $"{Guid.NewGuid():N}@example.com", "correct horse battery"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_a_branch_and_then_a_device_paired_to_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);

        var branchResponse = await client.PostAsJsonAsync("/branches", new CreateBranchRequest("Second Branch", "123 Rizal St."));
        Assert.Equal(HttpStatusCode.OK, branchResponse.StatusCode);
        var branch = await branchResponse.Content.ReadFromJsonAsync<BranchDto>(JsonOptions);

        var deviceResponse = await client.PostAsJsonAsync("/devices/pairing-requests", new CreateDevicePairingRequest("Tablet-2", DeviceType.Register, branch!.Id));
        Assert.Equal(HttpStatusCode.OK, deviceResponse.StatusCode);
        var device = (await deviceResponse.Content.ReadFromJsonAsync<DevicePairingCodeDto>(JsonOptions))!;

        Assert.Equal(branch.Id, device.Device.BranchId);
        Assert.NotEmpty(device.PairingCode);
    }

    [Fact]
    public async Task Creating_a_device_for_a_nonexistent_branch_returns_404()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/devices/pairing-requests", new CreateDevicePairingRequest("Ghost", DeviceType.Register, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_update_branding_and_bir_settings()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);

        var brandingResponse = await client.PutAsJsonAsync(
            "/tenant/settings/branding",
            new UpdateBrandingRequest(
                "https://cdn.example.com/logo.png",
                "#FFFFFF",
                "#4F46E5",
                "#111827",
                "#6B7280",
                "Inter",
                null));
        Assert.Equal(HttpStatusCode.OK, brandingResponse.StatusCode);
        var afterBranding = await brandingResponse.Content.ReadFromJsonAsync<TenantSettingsDto>(JsonOptions);
        Assert.Equal("#FFFFFF", afterBranding!.BrandingBackgroundColorHex);
        Assert.Equal("#4F46E5", afterBranding.BrandingAccentColorHex);
        Assert.Equal("#111827", afterBranding.BrandingPrimaryTextColorHex);
        Assert.Equal("#6B7280", afterBranding.BrandingSecondaryTextColorHex);

        var birResponse = await client.PutAsJsonAsync(
            "/tenant/settings/bir",
            new UpdateBirSettingsRequest("123-456-789-000", "Ana's Sari-Sari Store", "123 Rizal St.", 365));
        Assert.Equal(HttpStatusCode.OK, birResponse.StatusCode);
        var afterBir = await birResponse.Content.ReadFromJsonAsync<TenantSettingsDto>(JsonOptions);
        Assert.Equal("123-456-789-000", afterBir!.Tin);
    }

    [Fact]
    public async Task Admin_can_toggle_the_credit_ledger_setting_and_it_defaults_to_off()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);

        var settings = await client.GetFromJsonAsync<TenantSettingsDto>("/tenant/settings", JsonOptions);
        Assert.False(settings!.CreditLedgerEnabled);

        var enableResponse = await client.PutAsJsonAsync(
            "/tenant/settings/credit-ledger",
            new UpdateCreditLedgerSettingRequest(true));
        Assert.Equal(HttpStatusCode.OK, enableResponse.StatusCode);
        var afterEnable = await enableResponse.Content.ReadFromJsonAsync<TenantSettingsDto>(JsonOptions);
        Assert.True(afterEnable!.CreditLedgerEnabled);

        var disableResponse = await client.PutAsJsonAsync(
            "/tenant/settings/credit-ledger",
            new UpdateCreditLedgerSettingRequest(false));
        var afterDisable = await disableResponse.Content.ReadFromJsonAsync<TenantSettingsDto>(JsonOptions);
        Assert.False(afterDisable!.CreditLedgerEnabled);
    }

    [Fact]
    public async Task Invalid_branding_color_is_rejected_with_400()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            "/tenant/settings/branding",
            new UpdateBrandingRequest(null, null, "not-a-hex-color", null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enabling_a_cash_drawer_without_a_printer_profile_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);

        var branches = await client.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions);
        var branchId = branches!.Single().Id;

        var response = await client.PutAsJsonAsync(
            $"/branches/{branchId}/hardware-settings",
            new UpdateBranchHardwareSettingsRequest(ReceiptPrinterProfile.None, true, CashDrawerPolicy.KickOnSaleOnly));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_set_the_manual_gcash_qr_for_a_branch()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);

        var branches = await client.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions);
        var branchId = branches!.Single().Id;

        var response = await client.PutAsJsonAsync(
            $"/branches/{branchId}/manual-gcash-qr",
            new UpdateManualGcashQrSettingsRequest(
                "https://cdn.example.com/gcash-qr.png",
                "Ana Dela Cruz",
                "0917-000-0000"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<BranchDto>(JsonOptions);
        Assert.Equal("https://cdn.example.com/gcash-qr.png", updated!.ManualGcashQrImageUrl);
        Assert.Equal("Ana Dela Cruz", updated.ManualGcashAccountName);
    }

    [Fact]
    public async Task Setting_a_manual_gcash_qr_image_with_no_account_name_or_number_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);

        var branches = await client.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions);
        var branchId = branches!.Single().Id;

        var response = await client.PutAsJsonAsync(
            $"/branches/{branchId}/manual-gcash-qr",
            new UpdateManualGcashQrSettingsRequest("https://cdn.example.com/gcash-qr.png", null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Audit_log_endpoint_is_reachable_by_admin_and_returns_an_empty_list_before_any_sensitive_action()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);

        var response = await client.GetAsync("/audit-logs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var logs = await response.Content.ReadFromJsonAsync<List<AuditLogDto>>(JsonOptions);
        Assert.Empty(logs!);
    }

    [Fact]
    public async Task Anonymous_requests_to_protected_onboarding_endpoints_are_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/staff/members");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record SessionBody(string AccessToken, string RefreshToken);
}
