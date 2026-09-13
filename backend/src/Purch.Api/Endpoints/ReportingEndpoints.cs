using Microsoft.AspNetCore.Mvc;
using Purch.Application.Reporting;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var reportGenerator = new[] { nameof(Role.Admin), nameof(Role.Manager) };

        // --- FR26 — BIR X-reading (mid-shift, re-runnable) / Z-reading (end-of-day, advances the reset counter) ---
        _ = app.MapPost("/reports/x-reading", async (
            IBirReadingService birReadingService,
            CancellationToken cancellationToken) =>
            Results.Ok(await birReadingService.GenerateXReadingAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        _ = app.MapPost("/reports/z-reading", async (
            IBirReadingService birReadingService,
            CancellationToken cancellationToken) =>
            Results.Ok(await birReadingService.GenerateZReadingAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        // --- F1 — sales dashboard ---
        _ = app.MapGet("/reports/sales-dashboard", async (
            Guid? branchId,
            ISalesDashboardService salesDashboardService,
            CancellationToken cancellationToken) =>
            Results.Ok(await salesDashboardService.GetDashboardAsync(branchId, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        // --- F3 — inventory reports ---
        _ = app.MapGet("/reports/inventory/movement-summary", async (
            Guid? branchId,
            [FromQuery(Name = "from")] DateTimeOffset fromUtc,
            [FromQuery(Name = "to")] DateTimeOffset toUtc,
            IInventoryReportService inventoryReportService,
            CancellationToken cancellationToken) =>
            Results.Ok(await inventoryReportService.GetMovementSummaryAsync(branchId, fromUtc, toUtc, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        _ = app.MapGet("/reports/inventory/low-stock-export.csv", async (
            IInventoryReportService inventoryReportService,
            CancellationToken cancellationToken) =>
            Results.Text(
                await inventoryReportService.GenerateLowStockReorderCsvAsync(cancellationToken),
                "text/csv"))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        // --- F4 — staff performance ---
        _ = app.MapGet("/reports/staff-performance", async (
            Guid? branchId,
            [FromQuery(Name = "from")] DateTimeOffset fromUtc,
            [FromQuery(Name = "to")] DateTimeOffset toUtc,
            IStaffPerformanceService staffPerformanceService,
            CancellationToken cancellationToken) =>
            Results.Ok(await staffPerformanceService.GetReportAsync(branchId, fromUtc, toUtc, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        // --- B6 — department/concessionaire split sales-attribution report ---
        _ = app.MapGet("/reports/department-sales", async (
            Guid? branchId,
            [FromQuery(Name = "from")] DateTimeOffset fromUtc,
            [FromQuery(Name = "to")] DateTimeOffset toUtc,
            IDepartmentSalesReportService departmentSalesReportService,
            CancellationToken cancellationToken) =>
            Results.Ok(await departmentSalesReportService.GetReportAsync(branchId, fromUtc, toUtc, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        return app;
    }
}
