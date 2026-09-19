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

        _ = app.MapPut("/transactions/cart/lines/{lineId:guid}", async (
            Guid lineId,
            UpdateTransactionLineRequest request,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.UpdateLineAsync(lineId, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapDelete("/transactions/cart/lines/{lineId:guid}", async (
            Guid lineId,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.RemoveLineAsync(lineId, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapPost("/transactions/cart/void", async (
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.VoidCartAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posSupervisor));

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
            if (request.SeniorPwdDiscountApplied && !posSupervisor.Any(user.IsInRole))
            {
                throw new Purch.Application.Common.Exceptions.ForbiddenException(
                    "Only a manager or admin can apply the Senior Citizen/PWD discount.");
            }

            return Results.Ok(await transactionService.CheckoutAsync(request, cancellationToken));
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
