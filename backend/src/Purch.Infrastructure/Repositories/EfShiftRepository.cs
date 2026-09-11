using Microsoft.EntityFrameworkCore;
using Purch.Application.Shifts;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfShiftRepository(PurchDbContext dbContext) : IShiftRepository
{
    public Task<Shift?> GetOpenByDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        return dbContext.Shifts
            .FirstOrDefaultAsync(shift => shift.DeviceId == deviceId && shift.Status == ShiftStatus.Open, cancellationToken);
    }

    public Task<Shift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Shifts.FirstOrDefaultAsync(shift => shift.Id == id, cancellationToken);
    }

    public void Add(Shift shift)
    {
        _ = dbContext.Shifts.Add(shift);
    }
}
