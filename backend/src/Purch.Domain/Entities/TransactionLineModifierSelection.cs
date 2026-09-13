using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>One chosen modifier (e.g. "No Ice") on a cart line — mirrors
/// TransactionLineComboSelection's join-row shape. The modifier's group is
/// reachable via ItemModifier.ModifierGroupId, so it isn't duplicated here.</summary>
public class TransactionLineModifierSelection : TenantScopedEntity
{
    public Guid TransactionLineId { get; set; }

    public Guid ItemModifierId { get; set; }
}
