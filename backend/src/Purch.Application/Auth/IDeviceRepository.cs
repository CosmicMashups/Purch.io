using Purch.Domain.Entities;

namespace Purch.Application.Auth;

public interface IDeviceRepository
{
    Task<Device?> FindByPairingCodeAsync(string pairingCode, CancellationToken cancellationToken = default);
}
