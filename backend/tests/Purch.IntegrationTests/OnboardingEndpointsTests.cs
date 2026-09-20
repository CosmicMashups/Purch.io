using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class OnboardingEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Bootstrap_then_login_with_the_returned_pairing_code_and_chosen_pin_succeeds()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var bootstrapResponse = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest("Ana's Sari-Sari", BusinessType.ConvenienceStore, "Main Branch", "Ana Reyes", "1234"));

        Assert.Equal(HttpStatusCode.OK, bootstrapResponse.StatusCode);
        var bootstrapResult = await bootstrapResponse.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions);
        Assert.NotNull(bootstrapResult);

        var loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(bootstrapResult!.DevicePairingCode, "1234"));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_with_missing_fields_returns_400_with_field_errors()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest(string.Empty, BusinessType.ConvenienceStore, string.Empty, "Ana", "1234"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_staff_but_a_cashier_is_forbidden_from_doing_so()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var (adminToken, _) = await BootstrapAndLoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createCashierResponse = await client.PostAsJsonAsync(
            "/staff",
            new CreateStaffRequest("Ben Cashier", Role.Cashier, ScopeType.Tenant, null, null, "5678"));
        Assert.Equal(HttpStatusCode.OK, createCashierResponse.StatusCode);

        var (pairingCode, staffId) = await GetLastCreatedUserLoginAsync(client);

        // Now act as the cashier we just created — they should be rejected from staff management.
        using var cashierClient = factory.CreateClient();
        var cashierToken = await LoginAsync(cashierClient, pairingCode, "5678");
        cashierClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierToken);

        var forbiddenResponse = await cashierClient.PostAsJsonAsync(
            "/staff",
            new CreateStaffRequest("Someone Else", Role.Cashier, ScopeType.Tenant, null, null, "9999"));

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("123456789")]
    [InlineData("12a4")]
    [InlineData("12 4")]
    public async Task A_staff_pin_must_be_four_to_eight_digits(string pin)
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var (adminToken, _) = await BootstrapAndLoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.PostAsJsonAsync(
            "/staff",
            new CreateStaffRequest("Bad Pin", Role.Cashier, ScopeType.Tenant, null, null, pin));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Two_staff_cannot_share_a_pin_because_login_could_not_tell_them_apart()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var (adminToken, _) = await BootstrapAndLoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // "1234" is the admin's own PIN.
        var duplicate = await client.PostAsJsonAsync(
            "/staff",
            new CreateStaffRequest("Copycat", Role.Cashier, ScopeType.Tenant, null, null, "1234"));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var distinct = await client.PostAsJsonAsync(
            "/staff",
            new CreateStaffRequest("Ben Cashier", Role.Cashier, ScopeType.Tenant, null, null, "5678"));
        Assert.Equal(HttpStatusCode.OK, distinct.StatusCode);
    }

    [Fact]
    public async Task The_admin_pin_chosen_at_bootstrap_must_be_digits()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest($"Tenant-{Guid.NewGuid():N}", BusinessType.ConvenienceStore, "Main Branch", "Admin User", "abcd"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_a_branch_and_then_a_device_paired_to_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var (adminToken, pairingCode) = await BootstrapAndLoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        _ = pairingCode;

        var branchResponse = await client.PostAsJsonAsync("/branches", new CreateBranchRequest("Second Branch", "123 Rizal St."));
        Assert.Equal(HttpStatusCode.OK, branchResponse.StatusCode);
        var branch = await branchResponse.Content.ReadFromJsonAsync<BranchDto>(JsonOptions);

        var deviceResponse = await client.PostAsJsonAsync("/devices", new CreateDeviceRequest(branch!.Id, "Tablet-2"));
        Assert.Equal(HttpStatusCode.OK, deviceResponse.StatusCode);
        var device = await deviceResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);

        Assert.Equal(branch.Id, device!.BranchId);
        Assert.NotEmpty(device.PairingCode);
    }

    [Fact]
    public async Task Creating_a_device_for_a_nonexistent_branch_returns_404()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var (adminToken, _) = await BootstrapAndLoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.PostAsJsonAsync("/devices", new CreateDeviceRequest(Guid.NewGuid(), null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_update_branding_and_bir_settings()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var (adminToken, _) = await BootstrapAndLoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

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
        using var client = factory.CreateClient();

        var (adminToken, _) = await BootstrapAndLoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

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
        using var client = factory.CreateClient();

        var (adminToken, _) = await BootstrapAndLoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.PutAsJsonAsync(
            "/tenant/settings/branding",
            new UpdateBrandingRequest(null, null, "not-a-hex-color", null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enabling_a_cash_drawer_without_a_printer_profile_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var (adminToken, _) = await BootstrapAndLoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

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
        using var client = factory.CreateClient();

        var (adminToken, _) = await BootstrapAndLoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

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
        using var client = factory.CreateClient();

        var (adminToken, _) = await BootstrapAndLoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

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
        using var client = factory.CreateClient();

        var (adminToken, _) = await BootstrapAndLoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

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

        var response = await client.GetAsync("/staff");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<(string AccessToken, string PairingCode)> BootstrapAndLoginAsAdminAsync(HttpClient client)
    {
        var bootstrapResponse = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest(
                $"Tenant-{Guid.NewGuid():N}",
                BusinessType.ConvenienceStore,
                "Main Branch",
                "Admin User",
                "1234"));

        var bootstrapResult = await bootstrapResponse.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions);
        var token = await LoginAsync(client, bootstrapResult!.DevicePairingCode, "1234");

        return (token, bootstrapResult.DevicePairingCode);
    }

    private static async Task<string> LoginAsync(HttpClient client, string pairingCode, string pin)
    {
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(pairingCode, pin));
        var body = await response.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);
        return body!.AccessToken;
    }

    /// <summary>
    /// Test-only convenience: since staff creation doesn't return a device pairing code
    /// (staff log into whatever device they're handed, not a device tied to their own
    /// account), reuse the admin's own device pairing code to exercise the cashier's login.
    /// </summary>
    private static async Task<(string pairingCode, Guid staffId)> GetLastCreatedUserLoginAsync(HttpClient client)
    {
        var staff = await client.GetFromJsonAsync<List<StaffDto>>("/staff", JsonOptions);
        var cashier = staff!.Single(s => s.Role == Role.Cashier);

        var devices = await client.GetFromJsonAsync<List<DeviceDto>>("/devices", JsonOptions);
        return (devices!.Single().PairingCode, cashier.Id);
    }

    private sealed record LoginResponseBody(string AccessToken);
}
