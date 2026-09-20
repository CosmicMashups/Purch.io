namespace Purch.Domain.Enums;

public enum BranchTransferStatus
{
    Pending,
    InTransit,
    Received,

    /// <summary>Called off before it arrived. Appended last so existing stored values keep their meaning.</summary>
    Cancelled,
}
