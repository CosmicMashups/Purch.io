using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Onboarding;

namespace Purch.Application.Reporting;

public sealed class DepartmentSalesReportService(
    IReportingRepository reportingRepository,
    IDepartmentRepository departmentRepository,
    IReportScopeResolver reportScopeResolver,
    ICurrentTenantProvider currentTenantProvider) : IDepartmentSalesReportService
{
    private const string GeneralDepartmentName = "General (no department)";

    public async Task<IReadOnlyList<DepartmentSalesSummaryDto>> GetReportAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var resolvedBranchId = await reportScopeResolver.ResolveBranchIdAsync(branchId, cancellationToken);

        var deptSales = await reportingRepository.GetDepartmentSalesAsync(resolvedBranchId, fromUtc, toUtc, cancellationToken);

        var departments = await departmentRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var departmentNamesById = departments.ToDictionary(department => department.Id, department => department.Name);

        return [.. deptSales
            .Select(s => new DepartmentSalesSummaryDto(
                s.DepartmentId,
                s.DepartmentId is { } departmentId && departmentNamesById.TryGetValue(departmentId, out var name)
                    ? name
                    : GeneralDepartmentName,
                s.Revenue))
            .OrderByDescending(dto => dto.Revenue)];
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("The department sales report requires an authenticated tenant context.");
}
