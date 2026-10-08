using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfDeviceRepository(PurchDbContext dbContext) : IDeviceRepository
{
    public Task<Device?> FindByPairingCodeAsync(string pairingCode, CancellationToken cancellationToken = default)
    {
        // The pairing code is what identifies the tenant here, so this cannot be tenant-filtered.
        return dbContext.Devices
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(device => device.PairingCode == pairingCode && device.Status == DeviceStatus.Active, cancellationToken);
    }

    public Task<Device?> FindByPairingCodeHashAsync(string pairingCodeHash, CancellationToken cancellationToken = default)
    {
        return dbContext.Devices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(device => device.PairingCodeHash == pairingCodeHash, cancellationToken);
    }

    public Task<Device?> GetByIdUnscopedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Devices.IgnoreQueryFilters().FirstOrDefaultAsync(device => device.Id == id, cancellationToken);
    }

    public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Devices.FirstOrDefaultAsync(device => device.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Device>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Devices
            .AsNoTracking()
            .Where(device => device.TenantId == tenantId && !device.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public void Add(Device device)
    {
        _ = dbContext.Devices.Add(device);
    }
}
