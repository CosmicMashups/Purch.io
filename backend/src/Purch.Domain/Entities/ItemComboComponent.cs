using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class ItemComboComponent : TenantScopedEntity
{
    public Guid ParentItemId { get; set; }

    public Guid ComponentCategoryId { get; set; }

    public string SlotLabel { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    public decimal? SubstitutionUpchargeAmount { get; set; }
}
