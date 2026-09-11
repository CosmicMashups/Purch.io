using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>
/// Utang (B7). Minimum PII by design: full name + phone required, address optional.
/// No government ID or other NPC "sensitive personal information" collected.
/// See docs/adr/0006-npc-registration-tracked-externally.md.
/// </summary>
public class CustomerCreditLedger : TenantScopedEntity
{
    public string CustomerFullName { get; set; } = string.Empty;

    public string CustomerPhoneNumber { get; set; } = string.Empty;

    public string? CustomerAddress { get; set; }

    public decimal Balance { get; set; }

    public decimal CreditLimit { get; set; }

    public DateOnly? DueDate { get; set; }

    public bool IsActive { get; set; } = true;
}
