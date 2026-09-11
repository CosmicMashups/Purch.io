namespace Purch.Application.Onboarding;

public interface IStaffService
{
    Task<IReadOnlyList<StaffDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<StaffDto> CreateAsync(CreateStaffRequest request, CancellationToken cancellationToken = default);

    Task<StaffDto> UpdateAsync(Guid staffId, UpdateStaffRequest request, CancellationToken cancellationToken = default);
}
