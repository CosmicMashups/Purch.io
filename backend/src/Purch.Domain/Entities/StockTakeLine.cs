using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

/// <summary>One item (ItemId) or standalone ingredient (InventoryItemId) in a stock take: exactly one is set.</summary>
public class StockTakeLine : TenantScopedEntity
{
    public Guid StockTakeId { get; set; }

    public Guid? ItemId { get; set; }

    public Guid? InventoryItemId { get; set; }

    /// <summary>Name at the time the take was created.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Category at the time the take was created (a catalog Category for items, an InventoryCategory for ingredients).</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>On-hand at the instant the count was entered; null until counted. Re-entering a count re-snapshots it.</summary>
    public decimal? ExpectedQty { get; set; }

    public decimal? CountedQty { get; set; }

    public StockTakeLineState State { get; set; } = StockTakeLineState.Uncounted;

    public Guid? CountedBy { get; set; }

    public DateTimeOffset? CountedAt { get; set; }

    /// <summary>CountedQty - ExpectedQty; the signed adjustment posted on apply.</summary>
    public decimal? VarianceQty { get; set; }

    public decimal? UnitCostSnapshot { get; set; }

    /// <summary>The Adjustment movement posted for this line on apply; null when nothing was posted.</summary>
    public Guid? MovementId { get; set; }
}
