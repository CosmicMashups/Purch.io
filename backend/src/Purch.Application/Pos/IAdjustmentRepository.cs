using Purch.Domain.Entities;

namespace Purch.Application.Pos;

public interface IAdjustmentRepository
{
    void Add(Adjustment adjustment);

    void AddReturnLine(AdjustmentReturnLine line);

    void AddReplacementLine(AdjustmentReplacementLine line);

    /// <summary>Every return line recorded against this transaction by any adjustment so far — so a new
    /// exchange can tell how much of an original line is still left to return.</summary>
    Task<IReadOnlyList<AdjustmentReturnLine>> ListReturnLinesByTransactionAsync(Guid originalTransactionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdjustmentReturnLine>> ListReturnLinesAsync(Guid adjustmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdjustmentReplacementLine>> ListReplacementLinesAsync(Guid adjustmentId, CancellationToken cancellationToken = default);
}
