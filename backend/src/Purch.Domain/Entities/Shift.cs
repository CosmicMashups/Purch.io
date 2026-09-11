using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

/// <summary>D7 — one cash-drawer session per device, opened by a cashier with a
/// starting float and closed with an actual cash count reconciled against what
/// cash sales say should be in the drawer.</summary>
public class Shift : TenantScopedEntity
{
    public Guid BranchId { get; set; }

    public Guid DeviceId { get; set; }

    public ShiftStatus Status { get; set; } = ShiftStatus.Open;

    public Guid OpenedByUserId { get; set; }

    public decimal OpeningCashAmount { get; set; }

    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? ClosedByUserId { get; set; }

    public decimal? ClosingCashAmount { get; set; }

    /// <summary>OpeningCashAmount + cash collected during the shift — what the drawer should hold.</summary>
    public decimal? ExpectedCashAmount { get; set; }

    /// <summary>ClosingCashAmount - ExpectedCashAmount. Zero means the count matched exactly.</summary>
    public decimal? VarianceAmount { get; set; }

    public string? HandoverNotes { get; set; }

    /// <summary>Set only when VarianceAmount is non-zero — the Admin/Manager who approved the discrepancy.</summary>
    public Guid? ApprovedByUserId { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }
}
