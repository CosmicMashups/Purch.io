using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Pos;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Reporting;

/// <summary>See BirReadingDto's doc comment — best-effort, pending real BIR
/// accreditation review (docs/adr/0005).</summary>
public sealed class BirReadingService(
    ITransactionRepository transactionRepository,
    IReceiptSequenceRepository receiptSequenceRepository,
    IDeviceRepository deviceRepository,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : IBirReadingService
{
    private const decimal VatRate = 0.12m;

    public async Task<BirReadingDto> GenerateXReadingAsync(CancellationToken cancellationToken = default)
    {
        var deviceId = CurrentDeviceId;
        var sequence = await receiptSequenceRepository.GetOrCreateTrackedAsync(CurrentTenantId, CurrentBranchId, deviceId, cancellationToken);

        return await BuildReadingAsync(BirReadingType.X, deviceId, sequence, advanceCounters: false, cancellationToken);
    }

    public async Task<BirReadingDto> GenerateZReadingAsync(CancellationToken cancellationToken = default)
    {
        var deviceId = CurrentDeviceId;
        var sequence = await receiptSequenceRepository.GetOrCreateTrackedAsync(CurrentTenantId, CurrentBranchId, deviceId, cancellationToken);

        var reading = await BuildReadingAsync(BirReadingType.Z, deviceId, sequence, advanceCounters: true, cancellationToken);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return reading;
    }

    private async Task<BirReadingDto> BuildReadingAsync(
        BirReadingType type,
        Guid deviceId,
        ReceiptSequence sequence,
        bool advanceCounters,
        CancellationToken cancellationToken)
    {
        var transactions = await transactionRepository.ListCompletedByDeviceInReceiptRangeAsync(
            deviceId, sequence.LastZReadingReceiptNumber, cancellationToken);

        var beginningReceiptNumber = transactions.Count > 0 ? transactions[0].ReceiptNumber : (long?)null;
        var endingReceiptNumber = transactions.Count > 0 ? transactions[^1].ReceiptNumber : (long?)null;

        var netSales = transactions.Sum(t => t.TotalAmount);
        var totalDiscounts = transactions.Sum(t => t.DiscountAmount);
        var promoDiscountTotal = transactions.Sum(t => t.PromoDiscountAmount);
        var seniorPwdDiscountTotal = totalDiscounts - promoDiscountTotal;
        var grossSales = netSales + totalDiscounts;

        // VAT computed off net sales (post-Senior/PWD discount, which is VAT-exempt under RA 9994) —
        // see the TODO(BIR-ACCREDITATION) on BirReadingDto for why this is best-effort, not final.
        var vatableSales = netSales / (1 + VatRate);
        var vatAmount = netSales - vatableSales;

        var lastZReadingAt = sequence.LastZReadingAt ?? DateTimeOffset.MinValue;
        var voided = await transactionRepository.ListVoidedByDeviceSinceAsync(deviceId, lastZReadingAt, cancellationToken);
        var voidedAmount = voided.Sum(t => t.TotalAmount);

        var oldGrandAccumulatedSales = sequence.GrandAccumulatedSales;
        var newGrandAccumulatedSales = oldGrandAccumulatedSales + netSales;
        var resetCounter = sequence.ZReadingResetCounter;

        if (advanceCounters)
        {
            sequence.GrandAccumulatedSales = newGrandAccumulatedSales;
            sequence.ZReadingResetCounter += 1;
            resetCounter = sequence.ZReadingResetCounter;
            sequence.LastZReadingReceiptNumber = endingReceiptNumber ?? sequence.LastZReadingReceiptNumber;
            sequence.LastZReadingAt = DateTimeOffset.UtcNow;
        }

        var device = await deviceRepository.GetByIdAsync(deviceId, cancellationToken);
        var machineIdentificationNumber = device?.MachineIdentificationNumber ?? $"PENDING-MIN-{deviceId.ToString()[..8].ToUpperInvariant()}";

        return new BirReadingDto(
            type,
            deviceId,
            machineIdentificationNumber,
            DateTimeOffset.UtcNow,
            beginningReceiptNumber,
            endingReceiptNumber,
            transactions.Count,
            grossSales,
            vatableSales,
            vatAmount,
            seniorPwdDiscountTotal,
            promoDiscountTotal,
            totalDiscounts,
            netSales,
            voided.Count,
            voidedAmount,
            oldGrandAccumulatedSales,
            newGrandAccumulatedSales,
            resetCounter);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("BIR readings require an authenticated tenant context.");

    private Guid CurrentDeviceId => currentActorProvider.DeviceId
        ?? throw new InvalidOperationException("BIR readings require an authenticated device context.");

    private Guid CurrentBranchId => currentActorProvider.BranchId
        ?? throw new InvalidOperationException("BIR readings require an authenticated device's branch.");
}
