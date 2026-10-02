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

    /// <summary>Exchanges processed on this device since the given time — what BirReadingService folds
    /// into a reading's net sales (see BirReadingDto.ExchangeAdjustmentsTotal).</summary>
    Task<AdjustmentTotals> GetTotalsByDeviceSinceAsync(Guid deviceId, DateTimeOffset since, CancellationToken cancellationToken = default);
}

public sealed record AdjustmentTotals(int Count, decimal PriceDifferenceTotal);
