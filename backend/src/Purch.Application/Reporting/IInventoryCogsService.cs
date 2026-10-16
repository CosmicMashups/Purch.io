namespace Purch.Application.Reporting;

public interface IInventoryCogsService
{
    /// <param name="groupBy">item, category or day.</param>
    Task<InventoryCogsDto> GetAsync(DateOnly fromDate, DateOnly toDate, string groupBy, CancellationToken cancellationToken = default);
}

public interface IInventoryCogsRepository
{
    /// <summary>Completed and Refunded sales completed in [fromUtc, toUtc), summed in the database per business day, item, category and
    /// open-item flag; voided, open and parked carts never appear, and neither do lines fully returned.</summary>
    Task<IReadOnlyList<CogsAggregateRow>> ListAggregatesAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);
}
