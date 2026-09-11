using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>Concessionaire mode (B6): own inventory attribution, split sales-attribution report.</summary>
public class Department : TenantScopedEntity
{
    public Guid BranchId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ConcessionaireContactInfo { get; set; }
}
