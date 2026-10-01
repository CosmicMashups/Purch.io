namespace Purch.Application.Pos;

public interface IAdjustmentService
{
    /// <summary>Records an exchange against a completed sale: some quantity is returned, some different
    /// item(s) are taken instead, and the price difference is settled. Always needs an Admin/Manager's
    /// approval. See AdjustmentService for the current scope limits.</summary>
    Task<AdjustmentDto> CreateExchangeAsync(Guid originalTransactionId, CreateExchangeRequest request, CancellationToken cancellationToken = default);
}
