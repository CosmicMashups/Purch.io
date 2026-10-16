using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;

namespace Purch.Application.Reporting;

public sealed class InventoryCogsService(
    IInventoryCogsRepository repository,
    IItemRepository itemRepository,
    ICategoryRepository categoryRepository,
    ICurrentTenantProvider currentTenantProvider) : IInventoryCogsService
{
    public async Task<InventoryCogsDto> GetAsync(DateOnly fromDate, DateOnly toDate, string groupBy, CancellationToken cancellationToken = default)
    {
        var tenantId = currentTenantProvider.TenantId
            ?? throw new InvalidOperationException("The COGS report requires an authenticated tenant context.");
        var mode = (groupBy ?? "item").ToLowerInvariant();
        if (mode is not ("item" or "category" or "day"))
        {
            throw new ValidationException("groupBy", "groupBy must be item, category or day.");
        }

        // Business days are UTC+8; the end date is inclusive.
        var fromUtc = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue), ReportTimeZone.Offset).ToUniversalTime();
        var toUtc = new DateTimeOffset(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), ReportTimeZone.Offset).ToUniversalTime();
        ReportRange.Validate(fromUtc, toUtc);

        var rows = await repository.ListAggregatesAsync(fromUtc, toUtc, cancellationToken);

        var items = (await itemRepository.ListByTenantAsync(tenantId, cancellationToken)).ToDictionary(i => i.Id, i => i.Name);
        var categories = (await categoryRepository.ListByTenantAsync(tenantId, cancellationToken)).ToDictionary(c => c.Id, c => c.Name);

        (Guid? Id, string Name) Key(CogsAggregateRow x) => mode switch
        {
            "day" => (null, x.Day),
            _ when x.IsOpenItem || (mode == "item" && x.ItemId is null) => (null, "Open items"),
            "category" => (x.CategoryId, x.CategoryId is { } c && categories.TryGetValue(c, out var cn) ? cn : "Uncategorised"),
            _ => (x.ItemId, items.GetValueOrDefault(x.ItemId!.Value, "(removed item)")),
        };

        var groups = rows
            .GroupBy(Key)
            .Select(g =>
            {
                var t = Totals(g);
                return new CogsGroupDto(g.Key.Id, g.Key.Name, g.Sum(x => x.Quantity), t.Cogs, t.KnownRevenue, t.Coverage, t.Excluded);
            })
            .OrderBy(g => mode == "day" ? 0m : -g.Cogs)
            .ThenBy(g => g.Name, StringComparer.Ordinal)
            .ToList();

        var total = Totals(rows);
        return new InventoryCogsDto(groups, total.Cogs, total.KnownRevenue, total.Coverage, total.Excluded);
    }

    private static (decimal Cogs, decimal KnownRevenue, decimal Coverage, int Excluded) Totals(IEnumerable<CogsAggregateRow> rows)
    {
        decimal cogs = 0m, known = 0m, all = 0m;
        var excluded = 0;
        foreach (var row in rows)
        {
            cogs += row.Cogs;
            known += row.KnownRevenue;
            all += row.Revenue;
            excluded += row.ExcludedLines;
        }

        return (cogs, known, ReportMath.Percent(known, all) ?? 0m, excluded);
    }
}
