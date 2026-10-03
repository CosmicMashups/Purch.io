namespace Purch.Application.Onboarding;

public interface IDeviceManagementService
{
    Task<IReadOnlyList<DeviceDto>> ListAsync(CancellationToken cancellationToken = default);
}
