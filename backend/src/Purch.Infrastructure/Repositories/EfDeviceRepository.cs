using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfDeviceRepository(PurchDbContext dbContext) : IDeviceRepository
{
    public Task<Device?> FindByPairingCodeAsync(string pairingCode, CancellationToken cancellationToken = default)
    {
        return dbContext.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(device => device.PairingCode == pairingCode, cancellationToken);
    }
}
