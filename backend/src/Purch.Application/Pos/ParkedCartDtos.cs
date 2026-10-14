namespace Purch.Application.Pos;

public sealed record ParkCartRequest(string? Label);

public sealed record ParkedCartSummaryDto(
    Guid Id,
    string? Label,
    DateTimeOffset ParkedAt,
    string? ParkedByName,
    int LineCount,
    decimal TotalAmount);

/// <summary>A line that changed (or was dropped) when a parked cart was resumed. Reason is "PriceChanged" or "Unavailable".</summary>
public sealed record ChangedLineDto(Guid LineId, string Reason);

/// <summary>A parked cart with its tenant's TTL, as the retention sweep sees it.</summary>
public sealed record ParkedCartForExpiry(Guid Id, Guid TenantId, DateTimeOffset? ParkedAt, int TtlHours, decimal TotalAmount);
