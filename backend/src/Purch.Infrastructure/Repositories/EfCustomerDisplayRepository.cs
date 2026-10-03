using Microsoft.EntityFrameworkCore;
using Purch.Application.Devices;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfCustomerDisplayRepository(PurchDbContext dbContext) : ICustomerDisplayRepository
{
    public Task<CustomerDisplayState?> GetByRegisterAsync(Guid registerDeviceId, CancellationToken cancellationToken = default)
    {
        return dbContext.CustomerDisplayStates.FirstOrDefaultAsync(s => s.RegisterDeviceId == registerDeviceId, cancellationToken);
    }

    public Task<bool> HasActiveDisplayAsync(Guid registerDeviceId, CancellationToken cancellationToken = default)
    {
        return dbContext.Devices.AnyAsync(
            d => d.LinkedRegisterDeviceId == registerDeviceId && d.DeviceType == DeviceType.CustomerDisplay && d.Status == DeviceStatus.Active,
            cancellationToken);
    }

    public void Add(CustomerDisplayState state)
    {
        _ = dbContext.CustomerDisplayStates.Add(state);
    }
}
