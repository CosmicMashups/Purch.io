using Purch.Application.Devices;
using Purch.Application.Pos;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

/// <summary>Order Board (pending order numbers for waiting customers) and Kitchen
/// Display (pending orders with line items, for kitchen staff) — both
/// unattended, read-only terminals over the same pending-kiosk-orders data
/// PosEndpoints already exposes to cashiers, just under their own roles so
/// neither display can ever reach a cart/payment endpoint.</summary>
public static class DeviceDisplayEndpoints
{
    public static IEndpointRouteBuilder MapDeviceDisplayEndpoints(this IEndpointRouteBuilder app)
    {
        var orderBoardOnly = new[] { nameof(Role.OrderBoard) };
        var kitchenDisplayOnly = new[] { nameof(Role.KitchenDisplay) };

        _ = app.MapPost("/order-board/session", async (
            UnattendedSessionRequest request,
            IUnattendedSessionService sessionService,
            CancellationToken cancellationToken) =>
        {
            var result = await sessionService.PairAsync(request, DeviceType.OrderBoard, Role.OrderBoard, cancellationToken);
            return MapSessionResult(result);
        }).AllowAnonymous();

        _ = app.MapGet("/order-board/pending", async (
            Guid branchId,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
        {
            var orders = await transactionService.ListPendingKioskOrdersAsync(branchId, cancellationToken);
            return Results.Ok(ExcludePickedUp(orders));
        }).RequireAuthorization(policy => policy.RequireRole(orderBoardOnly));

        _ = app.MapPost("/kitchen-display/session", async (
            UnattendedSessionRequest request,
            IUnattendedSessionService sessionService,
            CancellationToken cancellationToken) =>
        {
            var result = await sessionService.PairAsync(request, DeviceType.KitchenDisplay, Role.KitchenDisplay, cancellationToken);
            return MapSessionResult(result);
        }).AllowAnonymous();

        _ = app.MapGet("/kitchen-display/pending", async (
            Guid branchId,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
        {
            var orders = await transactionService.ListPendingKioskOrdersAsync(branchId, cancellationToken);
            return Results.Ok(ExcludePickedUp(orders));
        }).RequireAuthorization(policy => policy.RequireRole(kitchenDisplayOnly));

        _ = app.MapPut("/kitchen-display/orders/{transactionId:guid}/status", async (
            Guid transactionId,
            UpdateKitchenStatusRequest request,
            ITransactionService transactionService,
            CancellationToken cancellationToken) =>
            Results.Ok(await transactionService.UpdateKitchenStatusAsync(transactionId, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(kitchenDisplayOnly));

        return app;
    }

    /// <summary>PickedUp orders are done as far as the kitchen/board workflow is
    /// concerned — they stay in the cashier's own kiosk-pending list (payment
    /// status is separate) but drop off these two live queues.</summary>
    private static IReadOnlyList<TransactionDto> ExcludePickedUp(IReadOnlyList<TransactionDto> orders) =>
        [.. orders.Where(order => order.KitchenStatus != KitchenStatus.PickedUp)];

    private static IResult MapSessionResult(UnattendedSessionResult result) => result switch
    {
        UnattendedSessionResult.Success success => Results.Ok(new { accessToken = success.AccessToken, refreshToken = success.RefreshToken }),
        UnattendedSessionResult.InvalidDevice => Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Invalid credentials.",
            detail: "The device pairing code or PIN was not recognized."),
        _ => throw new InvalidOperationException($"Unhandled {nameof(UnattendedSessionResult)} case: {result.GetType().Name}"),
    };
}
