using Purch.Domain.Enums;

namespace Purch.Application.Orders;

/// <summary>Which sales the Orders page shows. Dates are UTC instants; the page turns the viewer's local days into these.</summary>
public sealed record OrdersQuery(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToExclusiveUtc,
    OrderStatusFilter? Status,
    Guid? BranchId,
    Guid? DeviceId,
    Guid? StaffUserId,
    PaymentMethod? Method,
    string? Search,
    int Page = 1,
    int PageSize = 25);

public enum OrderStatusFilter
{
    Completed,
    Voided,
    Refunded,
    Exchanged,
}

/// <summary>One row of the Orders list. Receipt numbers repeat across devices, so the device is always part of the row.</summary>
public sealed record OrderListItemDto(
    Guid Id,
    long? ReceiptNumber,
    DateTimeOffset At,
    string Status,
    bool HasExchange,
    Guid BranchId,
    string BranchName,
    Guid DeviceId,
    string DeviceName,
    string? StaffName,
    string? CustomerName,
    IReadOnlyList<string> PaymentMethods,
    decimal TotalAmount);

public sealed record OrdersPageDto(IReadOnlyList<OrderListItemDto> Items, int Total, int Page, int PageSize);

public interface IOrdersQueryService
{
    Task<OrdersPageDto> ListAsync(OrdersQuery query, CancellationToken cancellationToken = default);

    /// <summary>Everything the query matches, not one page, as CSV text. Capped so one request can't read the whole table.</summary>
    Task<string> ExportCsvAsync(OrdersQuery query, CancellationToken cancellationToken = default);

    /// <summary>Throws NotFound for a sale outside the caller's branch, so the detail view can't be used to read another branch.</summary>
    Task EnsureVisibleAsync(Guid transactionId, CancellationToken cancellationToken = default);
}
