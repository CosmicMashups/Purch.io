namespace Purch.Application.Onboarding;

public interface IDepartmentService
{
    Task<IReadOnlyList<DepartmentDto>> ListForBranchAsync(Guid branchId, CancellationToken cancellationToken = default);

    Task<DepartmentDto> CreateAsync(
        Guid branchId,
        CreateDepartmentRequest request,
        CancellationToken cancellationToken = default);
}
