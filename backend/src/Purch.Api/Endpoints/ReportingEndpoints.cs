using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Purch.Application.Approvals;
using Purch.Application.Auth;
using Purch.Application.Reporting;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public static class ReportingEndpoints
{
    private static readonly TimeSpan DashboardCacheTtl = TimeSpan.FromSeconds(30);

    // Per instance and bounded; a serverless instance's copy simply dies with it.
    private static readonly MemoryCache DashboardCache =
        new(new MemoryCacheOptions { SizeLimit = 500 });

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
        // The dashboard is a handful of aggregations over the sales tables and is asked for on every refresh by every
        // open admin screen. A short per-caller cache absorbs that; ?fresh=true skips it (a manual Refresh button).
        _ = app.MapGet("/reports/sales-dashboard", async (
            HttpContext http,
            Guid? branchId,
            [FromQuery(Name = "from")] DateTimeOffset? fromUtc,
            [FromQuery(Name = "to")] DateTimeOffset? toUtc,
            bool? fresh,
            ISalesDashboardService salesDashboardService,
            CancellationToken cancellationToken) =>
        {
            // Keyed by tenant and user: the result is branch-scoped by the caller's role, so it must never be shared.
            var key = string.Join(
                '|',
                http.User.FindFirst(JwtClaimTypes.TenantId)?.Value,
                http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
                http.User.FindFirst(JwtClaimTypes.Role)?.Value,
                http.User.FindFirst(JwtClaimTypes.ScopeType)?.Value,
                http.User.FindFirst(JwtClaimTypes.ScopeId)?.Value,
                http.User.FindFirst(JwtClaimTypes.BranchId)?.Value,
                branchId,
                fromUtc?.UtcTicks,
                toUtc?.UtcTicks);
            if (fresh != true && DashboardCache.TryGetValue(key, out SalesDashboardDto? cached) && cached is not null)
            {
                return Results.Ok(cached);
            }

            var dashboard = await salesDashboardService.GetDashboardAsync(branchId, fromUtc, toUtc, cancellationToken);
            _ = DashboardCache.Set(key, dashboard, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = DashboardCacheTtl });
            return Results.Ok(dashboard);
        })
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

        _ = app.MapGet("/reports/inventory/valuation", async (
            Guid? categoryId,
            string? type,
            IInventoryValuationService valuationService,
            CancellationToken cancellationToken) =>
        {
            var kind = (type ?? "all").ToLowerInvariant();
            return kind is "all" or "item" or "ingredient"
                ? Results.Ok(await valuationService.GetAsync(new InventoryValuationQuery(categoryId, kind), cancellationToken))
                : Results.BadRequest(new { error = "type must be item, ingredient or all." });
        })
            .RequireAuthorization(policy => policy.RequireRole(nameof(Role.Admin), nameof(Role.Manager), nameof(Role.Warehouse)));

        // --- Data export: the tenant's own sales record, independent of this app (accounting, a
        // system switch, or a Data Privacy Act/GDPR access request). Admin-only — broader than the
        // reporting roles above, since it hands over raw rows rather than an aggregate. ---
        _ = app.MapGet("/reports/sales/transactions-export.csv", async (
            Guid? branchId,
            [FromQuery(Name = "from")] DateTimeOffset fromUtc,
            [FromQuery(Name = "to")] DateTimeOffset toUtc,
            ITransactionExportService transactionExportService,
            CancellationToken cancellationToken) =>
            Results.Text(
                await transactionExportService.GenerateTransactionsCsvAsync(branchId, fromUtc, toUtc, cancellationToken),
                "text/csv"))
            .RequireAuthorization(policy => policy.RequireRole(nameof(Role.Admin)));

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

        // --- U4 — payment type / category / hour in one response (Sales mix tab) ---
        _ = app.MapGet("/reports/sales-mix", async (
            Guid? branchId,
            [FromQuery(Name = "from")] DateTimeOffset fromUtc,
            [FromQuery(Name = "to")] DateTimeOffset toUtc,
            ISalesMixReportService salesMixReportService,
            CancellationToken cancellationToken) =>
            Results.Ok(await salesMixReportService.GetReportAsync(branchId, fromUtc, toUtc, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        // --- U4 — tax, margin, discounts, voids, refunds, dashboard insights ---
        _ = app.MapGet("/reports/tax", async (
            Guid? branchId,
            [FromQuery(Name = "from")] DateTimeOffset fromUtc,
            [FromQuery(Name = "to")] DateTimeOffset toUtc,
            IRevenueReportService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetTaxAsync(branchId, fromUtc, toUtc, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        _ = app.MapGet("/reports/gross-profit", async (
            Guid? branchId,
            [FromQuery(Name = "from")] DateTimeOffset fromUtc,
            [FromQuery(Name = "to")] DateTimeOffset toUtc,
            IRevenueReportService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetProfitAsync(branchId, fromUtc, toUtc, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        _ = app.MapGet("/reports/discounts", async (
            Guid? branchId,
            [FromQuery(Name = "from")] DateTimeOffset fromUtc,
            [FromQuery(Name = "to")] DateTimeOffset toUtc,
            IRevenueReportService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetDiscountsAsync(branchId, fromUtc, toUtc, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        _ = app.MapGet("/reports/discounts/manual", async (
            Guid? branchId,
            [FromQuery(Name = "from")] DateTimeOffset fromUtc,
            [FromQuery(Name = "to")] DateTimeOffset toUtc,
            int? offset,
            int? limit,
            IRevenueReportService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListManualDiscountsAsync(branchId, fromUtc, toUtc, offset ?? 0, limit ?? 50, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        _ = app.MapGet("/reports/voids", async (
            Guid? branchId,
            [FromQuery(Name = "from")] DateTimeOffset fromUtc,
            [FromQuery(Name = "to")] DateTimeOffset toUtc,
            int? offset,
            int? limit,
            IRevenueReportService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetVoidsAsync(branchId, fromUtc, toUtc, offset ?? 0, limit ?? 50, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        _ = app.MapGet("/reports/refunds", async (
            Guid? branchId,
            [FromQuery(Name = "from")] DateTimeOffset fromUtc,
            [FromQuery(Name = "to")] DateTimeOffset toUtc,
            int? offset,
            int? limit,
            IRevenueReportService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetRefundsAsync(branchId, fromUtc, toUtc, offset ?? 0, limit ?? 50, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        _ = app.MapGet("/reports/dashboard-insights", async (
            Guid? branchId,
            [FromQuery(Name = "from")] DateTimeOffset fromUtc,
            [FromQuery(Name = "to")] DateTimeOffset toUtc,
            IRevenueReportService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetInsightsAsync(branchId, fromUtc, toUtc, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        // Per-transaction listings name staff and approvers, so like the transactions export they are Admin only.
        _ = app.MapGet("/reports/voids.csv", async (
            Guid? branchId,
            [FromQuery(Name = "from")] DateTimeOffset fromUtc,
            [FromQuery(Name = "to")] DateTimeOffset toUtc,
            IRevenueReportService service,
            CancellationToken cancellationToken) =>
            Results.Text(await service.ExportVoidsCsvAsync(branchId, fromUtc, toUtc, cancellationToken), "text/csv"))
            .RequireAuthorization(policy => policy.RequireRole(nameof(Role.Admin)));

        _ = app.MapGet("/reports/refunds.csv", async (
            Guid? branchId,
            [FromQuery(Name = "from")] DateTimeOffset fromUtc,
            [FromQuery(Name = "to")] DateTimeOffset toUtc,
            IRevenueReportService service,
            CancellationToken cancellationToken) =>
            Results.Text(await service.ExportRefundsCsvAsync(branchId, fromUtc, toUtc, cancellationToken), "text/csv"))
            .RequireAuthorization(policy => policy.RequireRole(nameof(Role.Admin)));

        _ = app.MapGet("/reports/discounts/manual.csv", async (
            Guid? branchId,
            [FromQuery(Name = "from")] DateTimeOffset fromUtc,
            [FromQuery(Name = "to")] DateTimeOffset toUtc,
            IRevenueReportService service,
            CancellationToken cancellationToken) =>
            Results.Text(await service.ExportManualDiscountsCsvAsync(branchId, fromUtc, toUtc, cancellationToken), "text/csv"))
            .RequireAuthorization(policy => policy.RequireRole(nameof(Role.Admin)));

        // --- Daily review of void/refund/kitchen-order-edit approvals, grouped by who approved them ---
        _ = app.MapGet("/reports/approvals-review", async (
            DateOnly? date,
            IApprovalsReviewService approvalsReviewService,
            CancellationToken cancellationToken) =>
            Results.Ok(await approvalsReviewService.GetDailyReviewAsync(date, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        return app;
    }
}
