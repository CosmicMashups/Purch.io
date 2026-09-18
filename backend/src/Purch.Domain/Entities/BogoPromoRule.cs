using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>Automatic, no-code "Buy N Take M" rule — buying TriggerQuantity of
/// TriggerItemId earns FreeQuantity of FreeItemId free. FreeItemId may equal
/// TriggerItemId for a classic same-item BOGO, or differ for a cross-item one.</summary>
public class BogoPromoRule : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public Guid TriggerItemId { get; set; }

    public int TriggerQuantity { get; set; } = 1;

    public Guid FreeItemId { get; set; }

    public int FreeQuantity { get; set; } = 1;

    public DateTimeOffset? StartsAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public bool IsActive { get; set; } = true;
}
