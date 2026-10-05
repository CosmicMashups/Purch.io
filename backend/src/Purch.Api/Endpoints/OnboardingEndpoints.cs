using Purch.Api.RateLimiting;
using Purch.Application.Devices;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public sealed record TokenBody(string Token);

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

        // --- Staff accounts (sign-in redesign): an Admin or Manager invites a person, who opens a single-use link or QR
        // code on their own phone to set a password and a personal PIN. No email is sent. A Manager can only deal with
        // staff; the service refuses anything that would create or change an Admin or Manager for a Manager. ---
        var adminOrManager = new[] { admin, nameof(Role.Manager) };

        _ = app.MapGet("/staff/members", async (IStaffEnrolmentService enrolmentService, CancellationToken cancellationToken) =>
            Results.Ok(await enrolmentService.ListMembersAsync(cancellationToken))).RequireAuthorization(policy => policy.RequireRole(adminOrManager));

        _ = app.MapPut("/staff/members/{memberId:guid}", async (
            Guid memberId,
            UpdateMemberRequest request,
            System.Security.Claims.ClaimsPrincipal user,
            IStaffEnrolmentService enrolmentService,
            CancellationToken cancellationToken) =>
            Results.Ok(await enrolmentService.UpdateMemberAsync(memberId, request, user.IsInRole(admin), cancellationToken))).RequireAuthorization(policy => policy.RequireRole(adminOrManager));

        _ = app.MapPost("/staff/members/{memberId:guid}/reset-link", async (
            Guid memberId,
            System.Security.Claims.ClaimsPrincipal user,
            IStaffEnrolmentService enrolmentService,
            CancellationToken cancellationToken) =>
            Results.Ok(await enrolmentService.CreateResetLinkAsync(memberId, user.IsInRole(admin), cancellationToken))).RequireAuthorization(policy => policy.RequireRole(adminOrManager));

        _ = app.MapGet("/staff/legacy", async (IStaffEnrolmentService enrolmentService, CancellationToken cancellationToken) =>
            Results.Ok(await enrolmentService.ListLegacyAsync(cancellationToken))).RequireAuthorization(policy => policy.RequireRole(adminOrManager));

        _ = app.MapGet("/staff/invites", async (IStaffEnrolmentService enrolmentService, CancellationToken cancellationToken) =>
            Results.Ok(await enrolmentService.ListInvitesAsync(cancellationToken))).RequireAuthorization(policy => policy.RequireRole(adminOrManager));

        _ = app.MapPost("/staff/invites", async (
            CreateInviteRequest request,
            System.Security.Claims.ClaimsPrincipal user,
            IStaffEnrolmentService enrolmentService,
            CancellationToken cancellationToken) =>
            Results.Ok(await enrolmentService.CreateInviteAsync(request, user.IsInRole(admin), cancellationToken))).RequireAuthorization(policy => policy.RequireRole(adminOrManager));

        _ = app.MapDelete("/staff/invites/{inviteId:guid}", async (
            Guid inviteId,
            IStaffEnrolmentService enrolmentService,
            CancellationToken cancellationToken) =>
        {
            await enrolmentService.RevokeInviteAsync(inviteId, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole(adminOrManager));

        // The person opening the link has no session yet.
        _ = app.MapPost("/enrol/preview", async (
            TokenBody body,
            IStaffEnrolmentService enrolmentService,
            CancellationToken cancellationToken) =>
        {
            var preview = await enrolmentService.PreviewAsync(body.Token, cancellationToken);
            return preview is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Link not usable.", detail: "This link was already used, was cancelled, or has expired. Ask for a new one.")
                : Results.Ok(preview);
        }).AllowAnonymous().RequireRateLimiting(RateLimiterPolicies.AuthSensitive);

        _ = app.MapPost("/enrol/redeem", async (
            RedeemInviteRequest request,
            IStaffEnrolmentService enrolmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await enrolmentService.RedeemAsync(request, cancellationToken);
            return result switch
            {
                RedeemInviteResult.Success success => Results.Ok(new { accessToken = success.AccessToken, refreshToken = success.RefreshToken }),
                RedeemInviteResult.InvalidLink => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Link not usable.", detail: "This link was already used, was cancelled, or has expired. Ask for a new one."),
                _ => throw new InvalidOperationException($"Unhandled {nameof(RedeemInviteResult)} case: {result.GetType().Name}"),
            };
        }).AllowAnonymous().RequireRateLimiting(RateLimiterPolicies.AuthSensitive);

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

        // --- A paired Register or Warehouse device: a person picks their name and types their own PIN to unlock it.
        // The device proves itself with its credential (in the body, never in a URL). A PIN is checked against that one
        // person, a few wrong ones lock that person out for a while, and the wrong-PIN answer says how many tries are left. ---
        _ = app.MapPost("/devices/roster", async (
            RosterRequest request,
            IDeviceUnlockService unlockService,
            CancellationToken cancellationToken) =>
        {
            var result = await unlockService.GetRosterAsync(request, cancellationToken);
            return result switch
            {
                RosterResult.Success success => Results.Ok(success.Roster),
                RosterResult.Invalid => Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Device not recognized.",
                    detail: "This device was revoked, needs to be paired again, or does not take staff sign-in."),
                _ => throw new InvalidOperationException($"Unhandled {nameof(RosterResult)} case: {result.GetType().Name}"),
            };
        }).AllowAnonymous().RequireRateLimiting(RateLimiterPolicies.Refresh);

        _ = app.MapPost("/devices/unlock", async (
            UnlockRequest request,
            IDeviceUnlockService unlockService,
            CancellationToken cancellationToken) =>
        {
            var result = await unlockService.UnlockAsync(request, cancellationToken);
            return result switch
            {
                UnlockResult.Success success => Results.Ok(new { accessToken = success.AccessToken, refreshToken = success.RefreshToken, person = success.Person }),
                UnlockResult.Invalid => Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Cannot sign in here.",
                    detail: "This person is not set up to work on this device."),
                UnlockResult.WrongPin wrong => Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Incorrect PIN.",
                    detail: wrong.AttemptsLeft == 1 ? "That PIN is not right. One try left." : $"That PIN is not right. {wrong.AttemptsLeft} tries left.",
                    extensions: new Dictionary<string, object?> { ["attemptsLeft"] = wrong.AttemptsLeft }),
                UnlockResult.Locked locked => Results.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Too many wrong PINs.",
                    detail: "This person is locked out for a few minutes. Ask a manager, or try again later.",
                    extensions: new Dictionary<string, object?> { ["lockedUntil"] = locked.Until }),
                _ => throw new InvalidOperationException($"Unhandled {nameof(UnlockResult)} case: {result.GetType().Name}"),
            };
        // Not the tight per-IP login limit: every till in a shop shares one address and staff unlock them all day. What
        // stops guessing is the lockout on the person (five wrong PINs), which counts however the tries arrive.
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

        // --- Department tracking toggle: off hides every department screen, field and report ---
        _ = app.MapPut("/tenant/settings/department-tracking", async (
            UpdateDepartmentTrackingSettingRequest request,
            ITenantSettingsService settingsService,
            CancellationToken cancellationToken) =>
            Results.Ok(await settingsService.UpdateDepartmentTrackingSettingAsync(request, cancellationToken))).RequireAuthorization(policy => policy.RequireRole(admin));

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
