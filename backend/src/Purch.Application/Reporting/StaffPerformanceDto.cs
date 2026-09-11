namespace Purch.Application.Reporting;

/// <summary>F4 — sales per cashier + shift attendance summary, both over the
/// same date range/branch scope.</summary>
public sealed record StaffPerformanceReportDto(
    IReadOnlyList<StaffSalesSummaryDto> Sales,
    IReadOnlyList<StaffShiftAttendanceDto> ShiftAttendance);

public sealed record StaffSalesSummaryDto(Guid StaffUserId, string StaffName, int TransactionCount, decimal TotalSales);

public sealed record StaffShiftAttendanceDto(
    Guid StaffUserId,
    string StaffName,
    int ShiftsOpened,
    int ShiftsWithVariance);
