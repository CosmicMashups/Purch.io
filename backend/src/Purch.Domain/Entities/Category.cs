using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class Category : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
