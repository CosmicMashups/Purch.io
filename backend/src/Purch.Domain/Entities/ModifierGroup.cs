using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class ModifierGroup : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public bool AllowMultipleSelection { get; set; }
}
