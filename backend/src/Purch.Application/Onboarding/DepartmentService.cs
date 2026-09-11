using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.Application.Onboarding;

public sealed class DepartmentService(
    IDepartmentRepository departmentRepository,
    IBranchRepository branchRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IDepartmentService
{
    public async Task<IReadOnlyList<DepartmentDto>> ListForBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        _ = await branchRepository.GetByIdAsync(branchId, cancellationToken)
            ?? throw new NotFoundException("Branch", branchId);

        var departments = await departmentRepository.ListByBranchAsync(branchId, cancellationToken);
        return [.. departments.Select(ToDto)];
    }

    public async Task<DepartmentDto> CreateAsync(
        Guid branchId,
        CreateDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Department name is required.");
        }

        _ = await branchRepository.GetByIdAsync(branchId, cancellationToken)
            ?? throw new NotFoundException("Branch", branchId);

        var department = new Department
        {
            TenantId = CurrentTenantId,
            BranchId = branchId,
            Name = request.Name.Trim(),
            ConcessionaireContactInfo = request.ConcessionaireContactInfo?.Trim(),
        };

        departmentRepository.Add(department);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(department);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Department management requires an authenticated tenant context.");

    private static DepartmentDto ToDto(Department department)
    {
        return new(department.Id, department.BranchId, department.Name, department.ConcessionaireContactInfo);
    }
}
