using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class Device : TenantScopedEntity
{
    public Guid BranchId { get; set; }

    public string PairingCode { get; set; } = string.Empty;

    public string? DeviceIdentifier { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }

    /// <summary>BIR accreditation's Machine Identification Number for this terminal — admin-entered once the unit is accredited; falls back to a device-ID-derived placeholder until then (see BirReadingService).</summary>
    public string? MachineIdentificationNumber { get; set; }
}
