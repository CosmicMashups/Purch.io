using Purch.Domain.Entities;

namespace Purch.Application.Shifts;

public interface IShiftRepository
{
    /// <summary>At most one Open shift per device at a time.</summary>
    Task<Shift?> GetOpenByDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);

    Task<Shift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Shift shift);
}
