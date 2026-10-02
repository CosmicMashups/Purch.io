using Purch.Application.Pos;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public static class PosEndpoints
{
    public static IEndpointRouteBuilder MapPosEndpoints(this IEndpointRouteBuilder app)
    {
        var posOperator = new[] { nameof(Role.Admin), nameof(Role.Manager), nameof(Role.Cashier) };
        // Voiding a sale and applying an after-the-fact discount are sensitive,
        // audit-trailed actions per docs/WORKFLOW.md §7 — owner/manager only.
        var posSupervisor = new[] { nameof(Role.Admin), nameof(Role.Manager) };

        // --- Cart engine — one in-progress Open transaction per device ---
        _ = app.MapGet("/transactions/cart", async (
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.GetOrCreateOpenCartAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapPost("/transactions/cart/lines", async (
            AddTransactionLineRequest request,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.AddLineAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapPost("/transactions/cart/lines/batch", async (
            AddLinesBatchRequest request,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.AddLinesBatchAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapPut("/transactions/cart/lines/{lineId:guid}", async (
            Guid lineId,
            UpdateTransactionLineRequest request,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.UpdateLineAsync(lineId, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapDelete("/transactions/cart/lines/{lineId:guid}", async (
            Guid lineId,
            string? approverPin,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.RemoveLineAsync(lineId, approverPin, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        // Void and refund are reachable by any POS role (a cashier must be able to ask), but always need a
        // different Admin/Manager's PIN inside the service itself — see ApproverAuthorizationService.
        _ = app.MapPost("/transactions/cart/void", async (
            VoidCartRequest? request,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.VoidCartAsync(request ?? new VoidCartRequest(null), cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        // Looking up an older sale by its receipt number, so it can be refunded or exchanged without
        // the cashier already having it open (e.g. from an earlier day or a different terminal).
        _ = app.MapGet("/transactions/by-receipt/{receiptNumber:long}", async (
            long receiptNumber,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.FindCompletedByReceiptNumberAsync(receiptNumber, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapPost("/transactions/{transactionId:guid}/refund", async (
            Guid transactionId,
            RefundTransactionRequest request,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.RefundTransactionAsync(transactionId, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        // Exchange: reachable by any POS role (a cashier must be able to ask), always needs a manager/
        // admin's PIN inside the service itself — see ApproverAuthorizationService.
        _ = app.MapPost("/transactions/{transactionId:guid}/exchange", async (
            Guid transactionId,
            CreateExchangeRequest request,
            IAdjustmentService adjustmentService,
            CancellationToken cancellationToken) =>
            Results.Ok(await adjustmentService.CreateExchangeAsync(transactionId, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapPost("/transactions/cart/payments", async (
            RecordPaymentRequest request,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.RecordPaymentAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapGet("/transactions/receipt-sequence", async (
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(new { lastIssuedNumber = await transactionService.GetLastIssuedReceiptNumberAsync(cancellationToken) }))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        // --- One-call checkout: the device builds the cart locally and sends the whole sale at
        // payment. Idempotent on SaleId, so a retry after a lost response cannot charge twice. ---
        _ = app.MapPost("/transactions/checkout", async (
            CheckoutRequest request,
            System.Security.Claims.ClaimsPrincipal user,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
        {
            // The cart endpoint that toggles this discount is Admin/Manager-only; a sale must not
            // be a way around that just because it arrives in one call.
            // An offline sale that names who rang it up is checked against that person instead (see
            // TransactionService.CheckoutAsync): it syncs under whoever is signed in later, who may be a
            // cashier even though a manager applied the discount at the counter.
            return request.SeniorPwdDiscountApplied && !posSupervisor.Any(user.IsInRole)
                && !(request.OfflineSale && request.RungByStaffId is not null)
                ? throw new Application.Common.Exceptions.ForbiddenException(
                    "Only a manager or admin can apply the Senior Citizen/PWD discount.")
                : Results.Ok(await transactionService.CheckoutAsync(request, cancellationToken));
        })
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapPut("/transactions/cart/senior-pwd-discount", async (
            ApplySeniorPwdDiscountRequest request,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.ApplySeniorPwdDiscountAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posSupervisor));

        _ = app.MapPut("/transactions/cart/promo-code", async (
            ApplyPromoCodeRequest request,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.ApplyPromoCodeAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapPut("/transactions/cart/order-type", async (
            SetOrderTypeRequest request,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.SetOrderTypeAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        // --- Kiosk order pickup — a cashier POS claims a pending kiosk order,
        // then finishes it through the exact same payment/discount pipeline above ---
        _ = app.MapGet("/transactions/kiosk-pending", async (
            Guid branchId,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.ListPendingKioskOrdersAsync(branchId, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapPost("/transactions/kiosk-pending/{transactionId:guid}/claim", async (
            Guid transactionId,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.ClaimKioskOrderAsync(transactionId, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        return app;
    }
}
