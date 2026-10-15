namespace Purch.Application.Inventory;

public interface IStockTakeService
{
    Task<StockTakeDto> CreateAsync(CreateStockTakeRequest request, CancellationToken cancellationToken = default);

    Task<StockTakeDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockTakeSummaryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<StockTakeDto> CountLineAsync(Guid takeId, Guid lineId, decimal? counted, bool skip, CancellationToken cancellationToken = default);

    Task<StockTakeDto> StartAsync(Guid id, CancellationToken cancellationToken = default);

    Task<StockTakeDto> ToReviewAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Posts the variances with an Admin/Manager approver PIN. Re-applying an Applied take returns it unchanged.</summary>
    Task<StockTakeDto> ApplyAsync(Guid id, string? approverPin, CancellationToken cancellationToken = default);

    Task<StockTakeDto> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}
