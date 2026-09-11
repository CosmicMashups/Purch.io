using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Onboarding;

namespace Purch.Application.Reporting;

public sealed class DepartmentSalesReportService(
    IReportingRepository reportingRepository,
    IItemRepository itemRepository,
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

        var transactions = await reportingRepository.ListCompletedTransactionsAsync(resolvedBranchId, fromUtc, toUtc, cancellationToken);
        var lines = await reportingRepository.ListLinesForTransactionsAsync([.. transactions.Select(t => t.Id)], cancellationToken);

        var items = await itemRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var departmentIdByItemId = items.ToDictionary(item => item.Id, item => item.DepartmentId);

        var departments = await departmentRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var departmentNamesById = departments.ToDictionary(department => department.Id, department => department.Name);

        return [.. lines
            .GroupBy(line => departmentIdByItemId.GetValueOrDefault(line.ItemId))
            .Select(group => new DepartmentSalesSummaryDto(
                group.Key,
                group.Key is { } departmentId && departmentNamesById.TryGetValue(departmentId, out var name)
                    ? name
                    : GeneralDepartmentName,
                group.Sum(line => line.LineTotal)))
            .OrderByDescending(dto => dto.Revenue)];
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("The department sales report requires an authenticated tenant context.");
}
