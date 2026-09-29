using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class AuditLog : TenantScopedEntity
{
    public Guid ActorUserId { get; set; }

    public AuditActionType ActionType { get; set; }

    public string TargetEntityType { get; set; } = string.Empty;

    public Guid TargetEntityId { get; set; }

    public string? BeforeStateJson { get; set; }

    public string? AfterStateJson { get; set; }

    /// <summary>Who approved this action (a manager/admin PIN — see ApproverAuthorizationService), when it
    /// needed one. Null for actions that never require approval. A first-class column rather than something
    /// parsed back out of AfterStateJson, so the approvals review report can query it directly.</summary>
    public Guid? ApprovedByUserId { get; set; }
}
