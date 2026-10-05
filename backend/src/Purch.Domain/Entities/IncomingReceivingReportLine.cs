using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class IncomingReceivingReportLine : TenantScopedEntity
{
    public Guid ReportId { get; set; }

    public Guid ItemId { get; set; }

    public decimal QuantityReceived { get; set; }

    public string Uom { get; set; } = "pc";

    public decimal UnitPrice { get; set; }

    public ReceivingCondition Condition { get; set; } = ReceivingCondition.Good;

    public ReceivingRemark Remark { get; set; } = ReceivingRemark.Accepted;
}
