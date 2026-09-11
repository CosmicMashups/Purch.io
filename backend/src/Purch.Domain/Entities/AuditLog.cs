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
}
