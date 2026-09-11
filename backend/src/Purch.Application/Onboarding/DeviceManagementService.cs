using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.Application.Onboarding;

public sealed class DeviceManagementService(
    IDeviceRepository deviceRepository,
    IBranchRepository branchRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IDeviceManagementService
{
    public async Task<IReadOnlyList<DeviceDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var devices = await deviceRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        return [.. devices.Select(ToDto)];
    }

    public async Task<DeviceDto> CreateAsync(CreateDeviceRequest request, CancellationToken cancellationToken = default)
    {
        // Confirms the branch both exists and belongs to this tenant (the query
        // filter makes a cross-tenant branch id come back null, same as everywhere else).
        _ = await branchRepository.GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new NotFoundException("Branch", request.BranchId);

        var device = new Device
        {
            TenantId = CurrentTenantId,
            BranchId = request.BranchId,
            DeviceIdentifier = request.DeviceIdentifier,
            PairingCode = PairingCodeGenerator.Generate(),
        };

        deviceRepository.Add(device);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(device);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Device management requires an authenticated tenant context.");

    private static DeviceDto ToDto(Device device)
    {
        return new(device.Id, device.BranchId, device.PairingCode, device.DeviceIdentifier, device.LastSeenAt);
    }
}
