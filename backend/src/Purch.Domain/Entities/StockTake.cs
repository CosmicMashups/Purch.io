using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

/// <summary>A counting session over a set of items/ingredients. Applying it posts one signed Adjustment per counted
/// line of (counted - expected at the moment of counting), never "set stock to counted", so sales rung up after a
/// line was counted survive the apply.</summary>
public class StockTake : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public StockTakeStatus Status { get; set; } = StockTakeStatus.Draft;

    /// <summary>When true, expected quantities are hidden from responses while the take is Counting.</summary>
    public bool Blind { get; set; }

    public StockTakeScopeKind ScopeKind { get; set; }

    /// <summary>The branch the adjustment movements are recorded against (stock itself is tenant-wide).</summary>
    public Guid BranchId { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? AppliedAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public string? Note { get; set; }

    public List<StockTakeLine> Lines { get; set; } = [];
}
