using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class Device : TenantScopedEntity
{
    public Guid BranchId { get; set; }

    public string PairingCode { get; set; } = string.Empty;

    public string? DeviceIdentifier { get; set; }

    /// <summary>Defaults to Register for backward compatibility with rows created
    /// before this field existed — an existing staff terminal is exactly that.</summary>
    public DeviceType DeviceType { get; set; } = DeviceType.Register;

    /// <summary>Required for every DeviceType except Register: an unattended
    /// device (Kiosk/OrderBoard/KitchenDisplay) needs a second factor beyond the
    /// pairing code alone, since that code alone is otherwise enough for a
    /// stranger to pair a rogue device as this tenant. Same IPinHasher staff
    /// PINs use.</summary>
    public string? PairingPinHash { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }

    /// <summary>BIR accreditation's Machine Identification Number for this terminal — admin-entered once the unit is accredited; falls back to a device-ID-derived placeholder until then (see BirReadingService).</summary>
    public string? MachineIdentificationNumber { get; set; }
}
