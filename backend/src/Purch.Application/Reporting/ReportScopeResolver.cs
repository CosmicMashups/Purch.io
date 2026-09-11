using Purch.Application.Common;
using Purch.Application.Onboarding;
using ScopeTypeEnum = Purch.Domain.Enums.ScopeType;

namespace Purch.Application.Reporting;

public sealed class ReportScopeResolver(
    ICurrentActorProvider currentActorProvider,
    IDepartmentRepository departmentRepository) : IReportScopeResolver
{
    public async Task<Guid?> ResolveBranchIdAsync(Guid? requestedBranchId, CancellationToken cancellationToken = default)
    {
        if (currentActorProvider.ScopeType == ScopeTypeEnum.Branch)
        {
            return currentActorProvider.ScopeId
                ?? throw new InvalidOperationException("Branch-scoped actor has no ScopeId.");
        }

        if (currentActorProvider.ScopeType == ScopeTypeEnum.Department)
        {
            var departmentId = currentActorProvider.ScopeId
                ?? throw new InvalidOperationException("Department-scoped actor has no ScopeId.");
            var department = await departmentRepository.GetByIdAsync(departmentId, cancellationToken)
                ?? throw new InvalidOperationException("Department-scoped actor's department no longer exists.");
            return department.BranchId;
        }

        return requestedBranchId;
    }
}
