using System.Globalization;
using System.Text;
using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Inventory;
using Purch.Domain.Enums;

namespace Purch.Application.Reporting;

public sealed class InventoryReportService(
    IReportingRepository reportingRepository,
    IItemRepository itemRepository,
    IItemStockService itemStockService,
    ICurrentTenantProvider currentTenantProvider,
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
        var tenantId = currentTenantProvider.TenantId
            ?? throw new InvalidOperationException("The low-stock export requires an authenticated tenant context.");
        var items = await itemRepository.ListByTenantAsync(tenantId, cancellationToken);

        // Unlike the dashboard's LowStockItems (which excludes zero-stock items
        // into its own OutOfStockCount bucket), a reorder report needs every item
        // at or under its threshold — an item at zero stock needs reordering too.
        var onHand = await itemStockService.GetOnHandAsync(items, tenantId, cancellationToken);
        var reorderItems = items
            .Where(item => item.IsActive && onHand.ContainsKey(item.Id) && item.LowStockThreshold is { } threshold && onHand[item.Id] <= threshold)
            .OrderBy(item => onHand[item.Id]);

        var csv = new StringBuilder();
        _ = csv.AppendLine("Item,Stock On Hand,Low Stock Threshold,Suggested Reorder Quantity");

        foreach (var item in reorderItems)
        {
            var threshold = item.LowStockThreshold!.Value;
            var suggestedReorderQuantity = Math.Max(threshold - onHand[item.Id], 0);
            _ = csv.AppendLine(string.Join(
                ',',
                CsvField(item.Name),
                onHand[item.Id].ToString(CultureInfo.InvariantCulture),
                threshold.ToString(CultureInfo.InvariantCulture),
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
