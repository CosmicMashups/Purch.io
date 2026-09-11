namespace Purch.Application.Onboarding;

public interface IBranchService
{
    Task<IReadOnlyList<BranchDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);

    Task<BranchDto> UpdateHardwareSettingsAsync(
        Guid branchId,
        UpdateBranchHardwareSettingsRequest request,
        CancellationToken cancellationToken = default);
}
