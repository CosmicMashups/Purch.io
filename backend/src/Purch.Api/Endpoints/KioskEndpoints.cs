using Purch.Application.Kiosk;
using Purch.Application.Pos;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

/// <summary>
/// E1–E4/E6's order-preparation-only flow — a kiosk terminal pairs anonymously
/// (POST /kiosk/session) and everything after that is gated to Role.Kiosk alone.
/// Deliberately reuses ITransactionService's existing cart engine (add/update/
/// remove line, combo/variant resolution) rather than duplicating it — a kiosk
/// device is, mechanically, just another Device with its own open cart. What
/// makes it a kiosk and not a cashier POS is which endpoints its token can
/// reach: no payment, no Senior/PWD discount, no promo code, no utang — those
/// all live under PosEndpoints' posOperator role list, which never includes
/// Kiosk. See TransactionService.SubmitKioskOrderAsync/ClaimKioskOrderAsync for
/// the handoff to a cashier POS.
/// </summary>
public static class KioskEndpoints
{
    public static IEndpointRouteBuilder MapKioskEndpoints(this IEndpointRouteBuilder app)
    {
        var kioskOnly = new[] { nameof(Role.Kiosk) };

        _ = app.MapPost("/kiosk/session", async (
            KioskSessionRequest request,
            IKioskSessionService kioskSessionService,
            CancellationToken cancellationToken) =>
        {
            var result = await kioskSessionService.PairAsync(request, cancellationToken);
            return result switch
            {
                KioskSessionResult.Success success => Results.Ok(new { accessToken = success.AccessToken }),
                KioskSessionResult.InvalidDevice => Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid device.",
                    detail: "The device pairing code was not recognized."),
                _ => throw new InvalidOperationException($"Unhandled {nameof(KioskSessionResult)} case: {result.GetType().Name}"),
            };
        }).AllowAnonymous();

        _ = app.MapGet("/kiosk/cart", async (
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.GetOrCreateOpenCartAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(kioskOnly));

        _ = app.MapPost("/kiosk/cart/lines", async (
            AddTransactionLineRequest request,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.AddLineAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(kioskOnly));

        _ = app.MapPut("/kiosk/cart/lines/{lineId:guid}", async (
            Guid lineId,
            UpdateTransactionLineRequest request,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.UpdateLineAsync(lineId, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(kioskOnly));

        _ = app.MapDelete("/kiosk/cart/lines/{lineId:guid}", async (
            Guid lineId,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.RemoveLineAsync(lineId, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(kioskOnly));

        _ = app.MapPut("/kiosk/cart/order-type", async (
            SetOrderTypeRequest request,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.SetOrderTypeAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(kioskOnly));

        _ = app.MapPost("/kiosk/cart/submit", async (
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.SubmitKioskOrderAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(kioskOnly));

        return app;
    }
}
