using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class Device : TenantScopedEntity
{
    public Guid BranchId { get; set; }

    public string PairingCode { get; set; } = string.Empty;

    public string? DeviceIdentifier { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }
}
