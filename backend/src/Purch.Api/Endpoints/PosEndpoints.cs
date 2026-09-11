using Purch.Application.Pos;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public static class PosEndpoints
{
    public static IEndpointRouteBuilder MapPosEndpoints(this IEndpointRouteBuilder app)
    {
        var posOperator = new[] { nameof(Role.Admin), nameof(Role.Manager), nameof(Role.Cashier) };

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
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapPost("/transactions/cart/payments", async (
            RecordPaymentRequest request,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.RecordPaymentAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        return app;
    }
}
