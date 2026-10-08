using System.Text;
using Purch.Application.Orders;
using Purch.Application.Pos;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

/// <summary>The back-office Orders page: browse every finished sale and open its receipt. Admin and Manager only;
/// a branch-scoped Manager sees their own branch (OrdersQueryService resolves that, not the request).</summary>
public static class OrdersEndpoints
{
    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        var adminOrManager = new[] { nameof(Role.Admin), nameof(Role.Manager) };

        _ = app.MapGet("/orders", async (
            [AsParameters] OrdersFilter filter,
            IOrdersQueryService orders,
            CancellationToken cancellationToken) =>
            Results.Ok(await orders.ListAsync(filter.ToQuery(), cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(adminOrManager));

        _ = app.MapGet("/orders/export.csv", async (
            [AsParameters] OrdersFilter filter,
            IOrdersQueryService orders,
            CancellationToken cancellationToken) =>
            Results.File(Encoding.UTF8.GetBytes(await orders.ExportCsvAsync(filter.ToQuery(), cancellationToken)), "text/csv", "orders.csv"))
            .RequireAuthorization(policy => policy.RequireRole(adminOrManager));

        _ = app.MapGet("/orders/{transactionId:guid}", async (
            Guid transactionId,
            IOrdersQueryService orders,
            ITransactionService transactions,
            CancellationToken cancellationToken) =>
        {
            await orders.EnsureVisibleAsync(transactionId, cancellationToken);
            return Results.Ok(await transactions.GetFinishedAsync(transactionId, cancellationToken));
        }).RequireAuthorization(policy => policy.RequireRole(adminOrManager));

        return app;
    }

    private sealed record OrdersFilter(
        DateTimeOffset? From,
        DateTimeOffset? To,
        OrderStatusFilter? Status,
        Guid? BranchId,
        Guid? DeviceId,
        Guid? StaffUserId,
        PaymentMethod? Method,
        string? Search,
        int? Page,
        int? PageSize)
    {
        public OrdersQuery ToQuery() => new(From, To, Status, BranchId, DeviceId, StaffUserId, Method, Search, Page ?? 1, PageSize ?? 25);
    }
}
