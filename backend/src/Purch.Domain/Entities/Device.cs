using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class Device : TenantScopedEntity, ISoftDeletable
{
    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Guid? DeletedByUserId { get; set; }

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

    /// <summary>What the Admin calls this device, for example "Front counter till".</summary>
    public string? Name { get; set; }

    public DeviceStatus Status { get; set; } = DeviceStatus.Active;

    /// <summary>SHA-256 of the one-time code that pairs this device, or null once it has been used or has expired.
    /// Replaces the permanent PairingCode and PairingPinHash, which stay until the old sign-in is removed.</summary>
    public string? PairingCodeHash { get; set; }

    public DateTimeOffset? PairingCodeExpiresAt { get; set; }

    public DateTimeOffset? PairedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>For a CustomerDisplay: the Register whose cart it mirrors.</summary>
    public Guid? LinkedRegisterDeviceId { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }

    /// <summary>Bumped whenever this device's pairing code or PIN is reset. Embedded
    /// in every access token issued for this device (see JwtTokenService) and checked
    /// on every authenticated request (see DeviceSessionValidationMiddleware) so an
    /// already-issued access token stops working immediately on reset, not just once
    /// its refresh token would otherwise have been used again.</summary>
    public int SessionVersion { get; set; }

    /// <summary>BIR accreditation's Machine Identification Number for this terminal — admin-entered once the unit is accredited; falls back to a device-ID-derived placeholder until then (see BirReadingService).</summary>
    public string? MachineIdentificationNumber { get; set; }

    /// <summary>Wrong approver PINs typed at this terminal since the last correct one or the last lockout —
    /// see ApproverAuthorizationService. Never reset by anything except a correct PIN or the lockout window
    /// elapsing, so a cashier can't out-wait a lock by retrying from a different login.</summary>
    public int ApproverPinFailedAttempts { get; set; }

    /// <summary>Set once ApproverPinFailedAttempts reaches the limit; every approval attempt at this
    /// terminal is refused until this passes, even with the correct PIN.</summary>
    public DateTimeOffset? ApproverPinLockedUntil { get; set; }
}
