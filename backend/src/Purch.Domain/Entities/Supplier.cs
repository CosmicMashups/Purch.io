using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class Supplier : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public string? ContactInfo { get; set; }

    public bool IsActive { get; set; } = true;
}
