using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>Automatic, no-code rule pricing one unit of ItemAId together with
/// one unit of ItemBId at a fixed ComboPrice. Applies once per matching pair
/// found in the cart — multiple pairs stack.</summary>
public class ComboPromoRule : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public Guid ItemAId { get; set; }

    public Guid ItemBId { get; set; }

    public decimal ComboPrice { get; set; }

    public DateTimeOffset? StartsAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public bool IsActive { get; set; } = true;
}
