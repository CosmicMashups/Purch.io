namespace Purch.Application.Reporting;

public interface IInventoryCogsService
{
    /// <param name="groupBy">item, category or day.</param>
    Task<InventoryCogsDto> GetAsync(DateOnly fromDate, DateOnly toDate, string groupBy, CancellationToken cancellationToken = default);
}

public interface IInventoryCogsRepository
{
    /// <summary>Completed and Refunded sales completed in [fromUtc, toUtc); voided, open and parked carts never appear.</summary>
    Task<IReadOnlyList<CogsLineRow>> ListLinesAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);
}
