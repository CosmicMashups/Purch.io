using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class BranchTransfer : TenantScopedEntity
{
    public Guid SourceBranchId { get; set; }

    public Guid DestinationBranchId { get; set; }

    public BranchTransferStatus Status { get; set; } = BranchTransferStatus.Pending;
}
