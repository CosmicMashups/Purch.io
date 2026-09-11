using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class ModifierGroup : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public bool AllowMultipleSelection { get; set; }

    /// <summary>When true, checkout (Phase 4) must require a selection from this group before the item can be added to the cart.</summary>
    public bool IsRequired { get; set; }
}
