using Purch.Application.Onboarding;
using Purch.Domain.Enums;

namespace Purch.Application.Devices;

/// <summary>Admin asks for a new device: its type, branch and (for a customer display) the Register it mirrors.</summary>
public sealed record CreateDevicePairingRequest(string Name, DeviceType DeviceType, Guid BranchId, Guid? LinkedRegisterDeviceId = null);

/// <summary>The one-time code is shown to the Admin once and never stored in plaintext.</summary>
public sealed record DevicePairingCodeDto(DeviceDto Device, string PairingCode, DateTimeOffset ExpiresAt);

public sealed record PairDeviceRequest(string PairingCode);

/// <summary>What a device keeps after pairing. The credential is shown once; only its hash is stored.</summary>
public sealed record PairedDeviceDto(string DeviceCredential, Guid DeviceId, Guid TenantId, Guid BranchId, DeviceType DeviceType, string? Name);

public sealed record DeviceSessionRequest(string DeviceCredential);

/// <summary>AccessToken is null for a device that needs a person to sign in (a Register or Warehouse device); the
/// device is known and active, but it has no access of its own. RefreshToken renews the access token the usual way,
/// so the client needs nothing special for a device session; it is revoked with the device.</summary>
public sealed record DeviceSessionDto(string? AccessToken, bool RequiresStaff, Guid DeviceId, Guid TenantId, Guid BranchId, DeviceType DeviceType, string? Name, string? RefreshToken = null);

public abstract record DevicePairResult
{
    public sealed record Success(PairedDeviceDto Device) : DevicePairResult;

    /// <summary>Unknown, already used, or expired. One case on purpose.</summary>
    public sealed record InvalidCode : DevicePairResult;
}

public abstract record DeviceSessionResult
{
    public sealed record Success(DeviceSessionDto Session) : DeviceSessionResult;

    /// <summary>Unknown credential, or the device was revoked or is waiting to be paired again.</summary>
    public sealed record Invalid : DeviceSessionResult;
}

public interface IDevicePairingService
{
    /// <summary>Admin: creates a Pending device and its one-time code, valid for <see cref="DevicePairingService.CodeLifetime"/>.</summary>
    Task<DevicePairingCodeDto> CreateAsync(CreateDevicePairingRequest request, CancellationToken cancellationToken = default);

    /// <summary>Admin: a fresh one-time code for a device that is still waiting, or to pair an existing device again. Pairing
    /// again ends the sessions and credential the device holds now.</summary>
    Task<DevicePairingCodeDto> NewPairingCodeAsync(Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>Admin: takes a device out of service. Its credential and any session already issued stop working at once.</summary>
    Task<DeviceDto> RevokeAsync(Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>Anonymous: the device enters the one-time code and receives its own long-lived credential.</summary>
    Task<DevicePairResult> PairAsync(PairDeviceRequest request, CancellationToken cancellationToken = default);

    /// <summary>Anonymous: the device presents its credential and receives a session.</summary>
    Task<DeviceSessionResult> StartSessionAsync(DeviceSessionRequest request, CancellationToken cancellationToken = default);
}
