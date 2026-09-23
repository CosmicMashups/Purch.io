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

        var staffSales = await reportingRepository.GetStaffSalesAsync(resolvedBranchId, fromUtc, toUtc, cancellationToken);
        var attendance = await reportingRepository.GetStaffShiftAttendanceAsync(resolvedBranchId, fromUtc, toUtc, cancellationToken);
        var staff = await userRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var staffNamesById = staff.ToDictionary(user => user.Id, user => user.Name);

        var sales = staffSales
            .Select(s => new StaffSalesSummaryDto(
                s.StaffUserId,
                staffNamesById.TryGetValue(s.StaffUserId, out var name) ? name : "(former staff)",
                s.TransactionCount,
                s.TotalSales))
            .OrderByDescending(dto => dto.TotalSales)
            .ToList();

        var shiftAttendance = attendance
            .Select(a => new StaffShiftAttendanceDto(
                a.StaffUserId,
                staffNamesById.TryGetValue(a.StaffUserId, out var name) ? name : "(former staff)",
                a.ShiftsOpened,
                a.ShiftsWithDiscrepancy))
            .OrderByDescending(dto => dto.ShiftsOpened)
            .ToList();

        return new StaffPerformanceReportDto(sales, shiftAttendance);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("The staff performance report requires an authenticated tenant context.");
}
