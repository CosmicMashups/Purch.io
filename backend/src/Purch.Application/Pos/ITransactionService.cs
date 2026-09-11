namespace Purch.Application.Pos;

public interface ITransactionService
{
    /// <summary>Returns the current device's open cart, creating one if none exists yet.</summary>
    Task<TransactionDto> GetOrCreateOpenCartAsync(CancellationToken cancellationToken = default);

    Task<TransactionDto> AddLineAsync(AddTransactionLineRequest request, CancellationToken cancellationToken = default);

    Task<TransactionDto> UpdateLineAsync(Guid lineId, UpdateTransactionLineRequest request, CancellationToken cancellationToken = default);

    Task<TransactionDto> RemoveLineAsync(Guid lineId, CancellationToken cancellationToken = default);

    Task<TransactionDto> VoidCartAsync(CancellationToken cancellationToken = default);
}
