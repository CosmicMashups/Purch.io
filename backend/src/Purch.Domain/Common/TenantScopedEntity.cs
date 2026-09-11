namespace Purch.Domain.Common;

public abstract class TenantScopedEntity : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
}
