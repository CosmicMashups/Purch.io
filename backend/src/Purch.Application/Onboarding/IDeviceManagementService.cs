namespace Purch.Application.Onboarding;

public interface IDeviceManagementService
{
    Task<IReadOnlyList<DeviceDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<DeviceDto> CreateAsync(CreateDeviceRequest request, CancellationToken cancellationToken = default);
}
