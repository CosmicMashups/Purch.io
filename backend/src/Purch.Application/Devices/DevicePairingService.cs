using System.Security.Cryptography;
using System.Text;
using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Devices;

public sealed class DevicePairingService(
    IDeviceRepository deviceRepository,
    IBranchRepository branchRepository,
    IDeviceCredentialRepository credentialRepository,
    IRefreshTokenService refreshTokenService,
    IJwtTokenService jwtTokenService,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IDevicePairingService
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    public async Task<DevicePairingCodeDto> CreateAsync(CreateDevicePairingRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Give the device a name, for example \"Front counter till\".");
        }

        _ = await branchRepository.GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new NotFoundException("Branch", request.BranchId);

        await ValidateLinkAsync(request.DeviceType, request.BranchId, request.LinkedRegisterDeviceId, cancellationToken);

        var device = new Device
        {
            TenantId = CurrentTenantId,
            BranchId = request.BranchId,
            DeviceType = request.DeviceType,
            Name = request.Name.Trim(),
            Status = DeviceStatus.Pending,
            LinkedRegisterDeviceId = request.LinkedRegisterDeviceId,

            // The permanent code and PIN of the old sign-in stay empty: this device only ever pairs with a one-time code.
            PairingCode = string.Empty,
        };

        var code = StageNewCode(device);
        deviceRepository.Add(device);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DevicePairingCodeDto(ToDto(device), code, device.PairingCodeExpiresAt!.Value);
    }

    public async Task<DevicePairingCodeDto> NewPairingCodeAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var device = await deviceRepository.GetByIdAsync(deviceId, cancellationToken)
            ?? throw new NotFoundException("Device", deviceId);

        // Pairing an active (or revoked) device again ends what it holds now, exactly like a revoke.
        if (device.Status != DeviceStatus.Pending)
        {
            await EndSessionsAsync(device, cancellationToken);
            device.Status = DeviceStatus.Pending;
            device.RevokedAt = null;
            device.PairedAt = null;
        }

        var code = StageNewCode(device);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DevicePairingCodeDto(ToDto(device), code, device.PairingCodeExpiresAt!.Value);
    }

    public async Task<DeviceDto> RevokeAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var device = await deviceRepository.GetByIdAsync(deviceId, cancellationToken)
            ?? throw new NotFoundException("Device", deviceId);

        await EndSessionsAsync(device, cancellationToken);
        device.Status = DeviceStatus.Revoked;
        device.RevokedAt = DateTimeOffset.UtcNow;
        device.PairingCodeHash = null;
        device.PairingCodeExpiresAt = null;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(device);
    }

    public async Task<DevicePairResult> PairAsync(PairDeviceRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PairingCode))
        {
            return new DevicePairResult.InvalidCode();
        }

        var device = await deviceRepository.FindByPairingCodeHashAsync(Hash(Normalize(request.PairingCode)), cancellationToken);
        if (device is not { Status: DeviceStatus.Pending } || device.PairingCodeExpiresAt is not { } expires || expires <= DateTimeOffset.UtcNow)
        {
            return new DevicePairResult.InvalidCode();
        }

        var rawCredential = GenerateSecret();
        credentialRepository.Add(new DeviceCredential
        {
            TenantId = device.TenantId,
            DeviceId = device.Id,
            CredentialHash = Hash(rawCredential),
        });

        // Single use: the code is gone the moment it is exchanged.
        device.PairingCodeHash = null;
        device.PairingCodeExpiresAt = null;
        device.Status = DeviceStatus.Active;
        device.PairedAt = DateTimeOffset.UtcNow;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return new DevicePairResult.Success(new PairedDeviceDto(rawCredential, device.Id, device.TenantId, device.BranchId, device.DeviceType, device.Name));
    }

    public async Task<DeviceSessionResult> StartSessionAsync(DeviceSessionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceCredential))
        {
            return new DeviceSessionResult.Invalid();
        }

        var credential = await credentialRepository.FindByHashAsync(Hash(request.DeviceCredential), cancellationToken);
        if (credential is null || credential.RevokedAt is not null)
        {
            return new DeviceSessionResult.Invalid();
        }

        var device = await deviceRepository.GetByIdUnscopedAsync(credential.DeviceId, cancellationToken);
        if (device is not { Status: DeviceStatus.Active })
        {
            return new DeviceSessionResult.Invalid();
        }

        credential.LastUsedAt = DateTimeOffset.UtcNow;
        device.LastSeenAt = credential.LastUsedAt;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = device.DeviceType switch
        {
            DeviceType.Kiosk => jwtTokenService.IssueKioskAccessToken(device),
            DeviceType.OrderBoard => jwtTokenService.IssueUnattendedAccessToken(device, Role.OrderBoard),
            DeviceType.KitchenDisplay => jwtTokenService.IssueUnattendedAccessToken(device, Role.KitchenDisplay),
            DeviceType.CustomerDisplay => jwtTokenService.IssueUnattendedAccessToken(device, Role.CustomerDisplay),

            // A Register and a Warehouse device are operated by a signed-in person; the device alone has no access.
            DeviceType.Register or DeviceType.WarehouseOfficer => null,
            _ => throw new InvalidOperationException($"Unhandled {nameof(DeviceType)}: {device.DeviceType}"),
        };

        var refreshToken = accessToken is null ? null : await refreshTokenService.IssueAsync(device.TenantId, null, device.Id, cancellationToken);
        return new DeviceSessionResult.Success(new DeviceSessionDto(accessToken, accessToken is null, device.Id, device.TenantId, device.BranchId, device.DeviceType, device.Name, refreshToken));
    }

    private async Task ValidateLinkAsync(DeviceType type, Guid branchId, Guid? linkedRegisterId, CancellationToken cancellationToken)
    {
        if (type != DeviceType.CustomerDisplay)
        {
            if (linkedRegisterId is not null)
            {
                throw new ValidationException(nameof(CreateDevicePairingRequest.LinkedRegisterDeviceId), "Only a customer display is linked to a Register.");
            }

            return;
        }

        if (linkedRegisterId is not { } registerId)
        {
            throw new ValidationException(nameof(CreateDevicePairingRequest.LinkedRegisterDeviceId), "Choose the Register this customer display shows.");
        }

        var register = await deviceRepository.GetByIdAsync(registerId, cancellationToken)
            ?? throw new NotFoundException("Device", registerId);
        if (register.DeviceType != DeviceType.Register || register.BranchId != branchId || register.Status == DeviceStatus.Revoked)
        {
            throw new ValidationException(nameof(CreateDevicePairingRequest.LinkedRegisterDeviceId), "Choose a Register in the same branch.");
        }
    }

    /// <summary>Ends every session this device has: its credential, its refresh tokens, and any access token already
    /// issued (the session version is part of every device token and is checked on each request).</summary>
    private async Task EndSessionsAsync(Device device, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var credential in await credentialRepository.ListActiveByDeviceAsync(device.Id, cancellationToken))
        {
            credential.RevokedAt = now;
        }

        device.SessionVersion++;
        await refreshTokenService.RevokeAllForDeviceAsync(device.Id, cancellationToken);
    }

    private static string StageNewCode(Device device)
    {
        var code = PairingCodeGenerator.Generate();
        device.PairingCodeHash = Hash(code);
        device.PairingCodeExpiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime);
        return code;
    }

    private static string Normalize(string code) => code.Trim().Replace("-", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();

    private static string GenerateSecret()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Device management requires an authenticated tenant context.");

    private static DeviceDto ToDto(Device device)
    {
        return new DeviceDto(
            device.Id,
            device.BranchId,
            device.PairingCode,
            device.DeviceIdentifier,
            device.DeviceType,
            device.LastSeenAt,
            device.Name,
            device.Status,
            device.PairedAt,
            device.PairingCodeExpiresAt,
            device.LinkedRegisterDeviceId);
    }
}
