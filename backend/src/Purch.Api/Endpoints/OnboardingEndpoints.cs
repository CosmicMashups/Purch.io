using Purch.Api.RateLimiting;
using Purch.Application.Devices;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public static class OnboardingEndpoints
{
    public static IEndpointRouteBuilder MapOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        // --- One-time tenant setup (A1–A3) — anonymous, since no tenant/user exists yet ---
        _ = app.MapPost("/onboarding/bootstrap", async (
            BootstrapTenantRequest request,
            IBootstrapTenantService bootstrapService,
            CancellationToken cancellationToken) =>
        {
            var result = await bootstrapService.BootstrapAsync(request, cancellationToken);
            return Results.Ok(result);
        }).AllowAnonymous().RequireRateLimiting(RateLimiterPolicies.AuthSensitive);

        // --- Staff & roles (A4) ---
        var admin = nameof(Role.Admin);

        _ = app.MapGet("/staff", async (IStaffService staffService, CancellationToken cancellationToken) =>
            Results.Ok(await staffService.ListAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(admin, nameof(Role.Manager)));

        _ = app.MapPost("/staff", async (
            CreateStaffRequest request,
            IStaffService staffService,
            CancellationToken cancellationToken) =>
            Results.Ok(await staffService.CreateAsync(request, cancellationToken))).RequireAuthorization(policy => policy.RequireRole(admin));

        _ = app.MapPut("/staff/{staffId:guid}", async (
            Guid staffId,
            UpdateStaffRequest request,
            IStaffService staffService,
            CancellationToken cancellationToken) =>
            Results.Ok(await staffService.UpdateAsync(staffId, request, cancellationToken))).RequireAuthorization(policy => policy.RequireRole(admin));

        // --- Branches & devices (A3) ---
        _ = app.MapGet("/branches", async (IBranchService branchService, CancellationToken cancellationToken) =>
            Results.Ok(await branchService.ListAsync(cancellationToken))).RequireAuthorization();

        _ = app.MapPost("/branches", async (
            CreateBranchRequest request,
            IBranchService branchService,
            CancellationToken cancellationToken) =>
            Results.Ok(await branchService.CreateAsync(request, cancellationToken))).RequireAuthorization(policy => policy.RequireRole(admin));

        _ = app.MapPut("/branches/{branchId:guid}/hardware-settings", async (
            Guid branchId,
            UpdateBranchHardwareSettingsRequest request,
            IBranchService branchService,
            CancellationToken cancellationToken) =>
            Results.Ok(await branchService.UpdateHardwareSettingsAsync(branchId, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(admin));

        // --- Manual GCash QR (D5) — a merchant-uploaded static QR Ph code, no gateway/fee ---
        _ = app.MapPut("/branches/{branchId:guid}/manual-gcash-qr", async (
            Guid branchId,
            UpdateManualGcashQrSettingsRequest request,
            IBranchService branchService,
            CancellationToken cancellationToken) =>
            Results.Ok(await branchService.UpdateManualGcashQrSettingsAsync(branchId, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(admin));

        // --- Departments / concessionaires (B6) ---
        _ = app.MapGet("/branches/{branchId:guid}/departments", async (
            Guid branchId,
            IDepartmentService departmentService,
            CancellationToken cancellationToken) =>
            Results.Ok(await departmentService.ListForBranchAsync(branchId, cancellationToken))).RequireAuthorization();

        _ = app.MapPost("/branches/{branchId:guid}/departments", async (
            Guid branchId,
            CreateDepartmentRequest request,
            IDepartmentService departmentService,
            CancellationToken cancellationToken) =>
            Results.Ok(await departmentService.CreateAsync(branchId, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(admin));

        _ = app.MapGet("/devices", async (IDeviceManagementService deviceService, CancellationToken cancellationToken) =>
            Results.Ok(await deviceService.ListAsync(cancellationToken))).RequireAuthorization(policy => policy.RequireRole(admin));

        _ = app.MapPost("/devices", async (
            CreateDeviceRequest request,
            IDeviceManagementService deviceService,
            CancellationToken cancellationToken) =>
            Results.Ok(await deviceService.CreateAsync(request, cancellationToken))).RequireAuthorization(policy => policy.RequireRole(admin));

        // Regenerates the pairing code, immediately invalidating the old one and revoking
        // any session already issued under it — for a device that's been lost/replaced.
        _ = app.MapPost("/devices/{deviceId:guid}/reset-pairing-code", async (
            Guid deviceId,
            IDeviceManagementService deviceService,
            CancellationToken cancellationToken) =>
            Results.Ok(await deviceService.ResetPairingCodeAsync(deviceId, cancellationToken))).RequireAuthorization(policy => policy.RequireRole(admin));

        _ = app.MapPost("/devices/{deviceId:guid}/reset-pairing-pin", async (
            Guid deviceId,
            ResetDevicePairingPinRequest request,
            IDeviceManagementService deviceService,
            CancellationToken cancellationToken) =>
            Results.Ok(await deviceService.ResetPairingPinAsync(deviceId, request.NewPin, cancellationToken))).RequireAuthorization(policy => policy.RequireRole(admin));

        // --- One-time device pairing (replaces the permanent pairing code and PIN above) ---
        // The Admin creates the device and is shown a code valid for ten minutes; the device exchanges it for its own
        // revocable credential. Revoking, or pairing again, ends every session the device holds.
        _ = app.MapPost("/devices/pairing-requests", async (
            CreateDevicePairingRequest request,
            IDevicePairingService pairingService,
            CancellationToken cancellationToken) =>
            Results.Ok(await pairingService.CreateAsync(request, cancellationToken))).RequireAuthorization(policy => policy.RequireRole(admin));

        _ = app.MapPost("/devices/{deviceId:guid}/pairing-code", async (
            Guid deviceId,
            IDevicePairingService pairingService,
            CancellationToken cancellationToken) =>
            Results.Ok(await pairingService.NewPairingCodeAsync(deviceId, cancellationToken))).RequireAuthorization(policy => policy.RequireRole(admin));

        _ = app.MapPost("/devices/{deviceId:guid}/revoke", async (
            Guid deviceId,
            IDevicePairingService pairingService,
            CancellationToken cancellationToken) =>
            Results.Ok(await pairingService.RevokeAsync(deviceId, cancellationToken))).RequireAuthorization(policy => policy.RequireRole(admin));

        _ = app.MapPost("/devices/pair", async (
            PairDeviceRequest request,
            IDevicePairingService pairingService,
            CancellationToken cancellationToken) =>
        {
            var result = await pairingService.PairAsync(request, cancellationToken);
            return result switch
            {
                DevicePairResult.Success success => Results.Ok(success.Device),
                DevicePairResult.InvalidCode => Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid pairing code.",
                    detail: "The code was not recognized, has already been used, or has expired."),
                _ => throw new InvalidOperationException($"Unhandled {nameof(DevicePairResult)} case: {result.GetType().Name}"),
            };
        }).AllowAnonymous().RequireRateLimiting(RateLimiterPolicies.AuthSensitive);

        _ = app.MapPost("/devices/session", async (
            DeviceSessionRequest request,
            IDevicePairingService pairingService,
            CancellationToken cancellationToken) =>
        {
            var result = await pairingService.StartSessionAsync(request, cancellationToken);
            return result switch
            {
                DeviceSessionResult.Success success => Results.Ok(success.Session),
                DeviceSessionResult.Invalid => Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Device not recognized.",
                    detail: "This device was revoked or needs to be paired again."),
                _ => throw new InvalidOperationException($"Unhandled {nameof(DeviceSessionResult)} case: {result.GetType().Name}"),
            };
        }).AllowAnonymous().RequireRateLimiting(RateLimiterPolicies.Refresh);

        // --- Tenant settings: branding (A2), BIR/compliance (A5), barcode requirement ---
        // Read-only for every signed-in user and device (a till prints the registered business details on receipts and
        // a kiosk shows the poster), so an Admin does not have to sign in on each device. Every change below stays Admin-only.
        _ = app.MapGet("/tenant/settings", async (ITenantSettingsService settingsService, CancellationToken cancellationToken) =>
            Results.Ok(await settingsService.GetAsync(cancellationToken))).RequireAuthorization();

        _ = app.MapPut("/tenant/settings/branding", async (
            UpdateBrandingRequest request,
            ITenantSettingsService settingsService,
            CancellationToken cancellationToken) =>
            Results.Ok(await settingsService.UpdateBrandingAsync(request, cancellationToken))).RequireAuthorization(policy => policy.RequireRole(admin));

        _ = app.MapPut("/tenant/settings/bir", async (
            UpdateBirSettingsRequest request,
            ITenantSettingsService settingsService,
            CancellationToken cancellationToken) =>
            Results.Ok(await settingsService.UpdateBirSettingsAsync(request, cancellationToken))).RequireAuthorization(policy => policy.RequireRole(admin));

        _ = app.MapPut("/tenant/settings/barcode", async (
            UpdateBarcodeSettingRequest request,
            ITenantSettingsService settingsService,
            CancellationToken cancellationToken) =>
            Results.Ok(await settingsService.UpdateBarcodeSettingAsync(request, cancellationToken))).RequireAuthorization(policy => policy.RequireRole(admin));

        // --- Credit ledger ("utang") toggle (B7) — full checkout enforcement (credit limits, due dates) is Phase 9 ---
        _ = app.MapPut("/tenant/settings/credit-ledger", async (
            UpdateCreditLedgerSettingRequest request,
            ITenantSettingsService settingsService,
            CancellationToken cancellationToken) =>
            Results.Ok(await settingsService.UpdateCreditLedgerSettingAsync(request, cancellationToken))).RequireAuthorization(policy => policy.RequireRole(admin));

        // --- Separate inventory tracking toggle — off by default: Cashier items keep using
        // Item.StockOnHand directly until an admin opts into InventoryItem + recipe tracking ---
        _ = app.MapPut("/tenant/settings/inventory-tracking", async (
            UpdateInventoryTrackingSettingRequest request,
            ITenantSettingsService settingsService,
            CancellationToken cancellationToken) =>
            Results.Ok(await settingsService.UpdateInventoryTrackingSettingAsync(request, cancellationToken))).RequireAuthorization(policy => policy.RequireRole(admin));

        // --- Security & access: audit log viewer (A6) ---
        _ = app.MapGet("/audit-logs", async (
            Guid? actorUserId,
            AuditActionType? actionType,
            DateTimeOffset? from,
            DateTimeOffset? to,
            DateTimeOffset? before,
            Guid? beforeId,
            int? limit,
            IAuditLogQueryService auditLogQueryService,
            CancellationToken cancellationToken) =>
        {
            var query = new AuditLogQuery(actorUserId, actionType, from, to, before, limit, beforeId);
            return Results.Ok(await auditLogQueryService.QueryAsync(query, cancellationToken));
        }).RequireAuthorization(policy => policy.RequireRole(admin, nameof(Role.Manager)));

        return app;
    }
}
