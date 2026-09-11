using Purch.Application.Auth;
using Purch.Application.Common;

namespace Purch.Application.Reporting;

public sealed class StaffPerformanceService(
    IReportingRepository reportingRepository,
    IUserRepository userRepository,
    IReportScopeResolver reportScopeResolver,
    ICurrentTenantProvider currentTenantProvider) : IStaffPerformanceService
{
    public async Task<StaffPerformanceReportDto> GetReportAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var resolvedBranchId = await reportScopeResolver.ResolveBranchIdAsync(branchId, cancellationToken);

        var transactions = await reportingRepository.ListCompletedTransactionsAsync(resolvedBranchId, fromUtc, toUtc, cancellationToken);
        var shifts = await reportingRepository.ListClosedShiftsInRangeAsync(resolvedBranchId, fromUtc, toUtc, cancellationToken);
        var staff = await userRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var staffNamesById = staff.ToDictionary(user => user.Id, user => user.Name);

        var sales = transactions
            .Where(t => t.StaffUserId is not null)
            .GroupBy(t => t.StaffUserId!.Value)
            .Select(group => new StaffSalesSummaryDto(
                group.Key,
                staffNamesById.TryGetValue(group.Key, out var name) ? name : "(former staff)",
                group.Count(),
                group.Sum(t => t.TotalAmount)))
            .OrderByDescending(dto => dto.TotalSales)
            .ToList();

        var shiftAttendance = shifts
            .GroupBy(shift => shift.OpenedByUserId)
            .Select(group => new StaffShiftAttendanceDto(
                group.Key,
                staffNamesById.TryGetValue(group.Key, out var name) ? name : "(former staff)",
                group.Count(),
                group.Count(shift => shift.VarianceAmount is not null and not 0)))
            .OrderByDescending(dto => dto.ShiftsOpened)
            .ToList();

        return new StaffPerformanceReportDto(sales, shiftAttendance);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("The staff performance report requires an authenticated tenant context.");
}
