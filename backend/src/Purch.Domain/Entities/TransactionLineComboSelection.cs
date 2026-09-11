using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class TransactionLineComboSelection : TenantScopedEntity
{
    public Guid TransactionLineId { get; set; }

    public Guid ItemComboComponentId { get; set; }

    public Guid SelectedItemId { get; set; }
}
