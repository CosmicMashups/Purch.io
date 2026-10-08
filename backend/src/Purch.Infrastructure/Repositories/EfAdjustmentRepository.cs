using Microsoft.EntityFrameworkCore;
using Purch.Application.Pos;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfAdjustmentRepository(PurchDbContext dbContext) : IAdjustmentRepository
{
    public void Add(Adjustment adjustment)
    {
        _ = dbContext.Adjustments.Add(adjustment);
    }

    public void AddReturnLine(AdjustmentReturnLine line)
    {
        _ = dbContext.AdjustmentReturnLines.Add(line);
    }

    public void AddReplacementLine(AdjustmentReplacementLine line)
    {
        _ = dbContext.AdjustmentReplacementLines.Add(line);
    }

    public async Task<IReadOnlyList<AdjustmentReturnLine>> ListReturnLinesByTransactionAsync(Guid originalTransactionId, CancellationToken cancellationToken = default)
    {
        return await (
                from line in dbContext.AdjustmentReturnLines.AsNoTracking()
                join adjustment in dbContext.Adjustments.AsNoTracking() on line.AdjustmentId equals adjustment.Id
                where adjustment.OriginalTransactionId == originalTransactionId
                select line)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdjustmentReturnLine>> ListReturnLinesAsync(Guid adjustmentId, CancellationToken cancellationToken = default)
    {
        return await dbContext.AdjustmentReturnLines.AsNoTracking().Where(line => line.AdjustmentId == adjustmentId).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdjustmentReplacementLine>> ListReplacementLinesAsync(Guid adjustmentId, CancellationToken cancellationToken = default)
    {
        return await dbContext.AdjustmentReplacementLines.AsNoTracking().Where(line => line.AdjustmentId == adjustmentId).ToListAsync(cancellationToken);
    }

    public async Task<decimal> SumCashSettlementsByDeviceSinceAsync(Guid deviceId, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        return await dbContext.Adjustments
            .AsNoTracking()
            .Where(adjustment => adjustment.DeviceId == deviceId
                && adjustment.CreatedAt >= since
                && adjustment.SettlementMethod == PaymentMethod.Cash)
            .SumAsync(adjustment => adjustment.PriceDifference, cancellationToken);
    }

    public async Task<AdjustmentTotals> GetTotalsByDeviceSinceAsync(Guid deviceId, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var row = await dbContext.Adjustments
            .AsNoTracking()
            .Where(adjustment => adjustment.DeviceId == deviceId && adjustment.CreatedAt >= since)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                PriceDifferenceTotal = g.Sum(a => a.PriceDifference),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? new AdjustmentTotals(0, 0m) : new AdjustmentTotals(row.Count, row.PriceDifferenceTotal);
    }
}
