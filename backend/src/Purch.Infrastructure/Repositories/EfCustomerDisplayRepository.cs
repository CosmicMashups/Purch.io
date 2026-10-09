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

    public async Task<CustomerDisplayFeedRead?> GetFeedForDisplayAsync(Guid displayDeviceId, CancellationToken cancellationToken = default)
    {
        var row = await (
            from display in dbContext.Devices.AsNoTracking()
            where display.Id == displayDeviceId
                && display.DeviceType == DeviceType.CustomerDisplay
                && display.Status == DeviceStatus.Active
                && display.LinkedRegisterDeviceId != null
            join state in dbContext.CustomerDisplayStates.AsNoTracking() on display.LinkedRegisterDeviceId equals state.RegisterDeviceId into states
            from state in states.DefaultIfEmpty()
            select new { State = state })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : new CustomerDisplayFeedRead(row.State);
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
