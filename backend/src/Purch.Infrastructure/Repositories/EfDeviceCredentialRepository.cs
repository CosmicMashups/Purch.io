using Microsoft.EntityFrameworkCore;
using Purch.Application.Devices;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfDeviceCredentialRepository(PurchDbContext dbContext) : IDeviceCredentialRepository
{
    public Task<DeviceCredential?> FindByHashAsync(string credentialHash, CancellationToken cancellationToken = default)
    {
        return dbContext.DeviceCredentials
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(credential => credential.CredentialHash == credentialHash, cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceCredential>> ListActiveByDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        return await dbContext.DeviceCredentials
            .IgnoreQueryFilters()
            .Where(credential => credential.DeviceId == deviceId && credential.RevokedAt == null)
            .ToListAsync(cancellationToken);
    }

    public void Add(DeviceCredential credential)
    {
        _ = dbContext.DeviceCredentials.Add(credential);
    }
}
