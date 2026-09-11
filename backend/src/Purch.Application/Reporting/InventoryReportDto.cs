using Purch.Domain.Enums;

namespace Purch.Application.Reporting;

/// <summary>F3 — stock movement summary by type over a date range.</summary>
public sealed record MovementSummaryDto(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<MovementTypeSummaryDto> ByType);

public sealed record MovementTypeSummaryDto(MovementType Type, decimal TotalQuantity, int MovementCount);
