using System.Globalization;
using System.Text;
using Purch.Application.Inventory;
using Purch.Domain.Enums;

namespace Purch.Application.Reporting;

public sealed class InventoryReportService(
    IReportingRepository reportingRepository,
    IInventoryDashboardService inventoryDashboardService,
    IReportScopeResolver reportScopeResolver) : IInventoryReportService
{
    public async Task<MovementSummaryDto> GetMovementSummaryAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var resolvedBranchId = await reportScopeResolver.ResolveBranchIdAsync(branchId, cancellationToken);
        var movements = await reportingRepository.ListMovementsInRangeAsync(resolvedBranchId, fromUtc, toUtc, cancellationToken);

        var byType = Enum.GetValues<MovementType>()
            .Select(type =>
            {
                var matching = movements.Where(movement => movement.Type == type).ToList();
                return new MovementTypeSummaryDto(
                    type,
                    matching.Sum(movement => movement.Quantity),
                    matching.Count);
            })
            .ToList();

        return new MovementSummaryDto(fromUtc, toUtc, byType);
    }

    public async Task<string> GenerateLowStockReorderCsvAsync(CancellationToken cancellationToken = default)
    {
        var dashboard = await inventoryDashboardService.GetDashboardAsync(cancellationToken);

        var csv = new StringBuilder();
        _ = csv.AppendLine("Item,Stock On Hand,Low Stock Threshold,Suggested Reorder Quantity");

        foreach (var item in dashboard.LowStockItems)
        {
            var suggestedReorderQuantity = Math.Max(item.LowStockThreshold - item.StockOnHand, 0);
            _ = csv.AppendLine(string.Join(
                ',',
                CsvField(item.ItemName),
                item.StockOnHand.ToString(CultureInfo.InvariantCulture),
                item.LowStockThreshold.ToString(CultureInfo.InvariantCulture),
                suggestedReorderQuantity.ToString(CultureInfo.InvariantCulture)));
        }

        return csv.ToString();
    }

    private static string CsvField(string value)
    {
        return value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
