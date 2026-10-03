using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Pos;

/// <summary>
/// Exchanges against an already-completed sale (decision B — the return/replacement Adjustment, linked to
/// the original receipt rather than editing it). Scope for now, all deliberate and documented at the
/// point each one is checked below:
///  - Replacement items must be plain Unit or VariantMatrix pricing — no combos (no slot pricing to
///    resolve), no WeightVolume (batch consumption isn't touched here), no Service (nothing to restock).
///  - Settlement is Cash, BankTransfer or ManualGcashQr only — no UtangCredit (would need the credit-limit
///    checks TransactionService.ChargeToCreditLedgerAsync has, not duplicated here).
///  - The price difference counts on the BIR X/Z-reading of the device that processed the exchange, in the
///    window it happened (see BirReadingService).
/// </summary>
public sealed class AdjustmentService(
    ITransactionRepository transactionRepository,
    IAdjustmentRepository adjustmentRepository,
    IItemRepository itemRepository,
    IItemVariantRepository itemVariantRepository,
    IItemStockService itemStockService,
    IInventoryMovementRepository inventoryMovementRepository,
    IAuditLogRepository auditLogRepository,
    IUserRepository userRepository,
    IApproverAuthorizationService approverAuthorizationService,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : IAdjustmentService
{
    private static readonly HashSet<PaymentMethod> SupportedSettlementMethods =
    [
        PaymentMethod.Cash,
        PaymentMethod.BankTransfer,
        PaymentMethod.ManualGcashQr,
    ];

    public async Task<AdjustmentDto> CreateExchangeAsync(Guid originalTransactionId, CreateExchangeRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ValidationException(nameof(request.Reason), "A reason is required for an exchange.");
        }

        if (request.ReturnLines.Count == 0)
        {
            throw new ValidationException(nameof(request.ReturnLines), "At least one line must be returned.");
        }

        if (request.ReplacementLines.Count == 0)
        {
            throw new ValidationException(nameof(request.ReplacementLines), "At least one replacement item is required.");
        }

        var transaction = await transactionRepository.GetByIdAsync(originalTransactionId, cancellationToken);
        if (transaction is null || transaction.TenantId != CurrentTenantId)
        {
            throw new NotFoundException("Transaction", originalTransactionId);
        }

        if (transaction.Status != TransactionStatus.Completed)
        {
            throw new ValidationException(nameof(transaction.Status), "Only a completed sale can be exchanged against.");
        }

        var originalLines = (await transactionRepository.ListLinesAsync(transaction.Id, cancellationToken)).ToDictionary(line => line.Id);
        var alreadyReturned = (await adjustmentRepository.ListReturnLinesByTransactionAsync(transaction.Id, cancellationToken))
            .GroupBy(line => line.OriginalLineId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));

        var returnLines = new List<AdjustmentReturnLine>();
        foreach (var requested in request.ReturnLines)
        {
            if (requested.Quantity <= 0)
            {
                throw new ValidationException(nameof(request.ReturnLines), "Return quantity must be greater than zero.");
            }

            if (!originalLines.TryGetValue(requested.OriginalLineId, out var originalLine))
            {
                throw new NotFoundException("Transaction line", requested.OriginalLineId);
            }

            var alreadyReturnedQuantity = alreadyReturned.GetValueOrDefault(requested.OriginalLineId);
            var remaining = originalLine.Quantity - alreadyReturnedQuantity;
            if (requested.Quantity > remaining)
            {
                throw new ValidationException(
                    nameof(request.ReturnLines),
                    $"Only {remaining} of that line is still available to return (the rest has already been returned).");
            }

            returnLines.Add(new AdjustmentReturnLine
            {
                TenantId = CurrentTenantId,
                OriginalLineId = originalLine.Id,
                ItemId = originalLine.ItemId,
                ItemVariantId = originalLine.ItemVariantId,
                Quantity = requested.Quantity,
                UnitPrice = originalLine.UnitPrice,
                LineTotal = originalLine.UnitPrice * requested.Quantity,
            });
        }

        var replacementLines = new List<AdjustmentReplacementLine>();
        foreach (var requested in request.ReplacementLines)
        {
            if (requested.Quantity <= 0)
            {
                throw new ValidationException(nameof(request.ReplacementLines), "Replacement quantity must be greater than zero.");
            }

            var item = await itemRepository.GetByIdAsync(requested.ItemId, cancellationToken)
                ?? throw new NotFoundException("Item", requested.ItemId);

            if (!item.IsActive)
            {
                throw new ValidationException(nameof(request.ReplacementLines), $"{item.Name} is not active.");
            }

            if (item.PricingType is not (PricingType.Unit or PricingType.VariantMatrix))
            {
                throw new ValidationException(
                    nameof(request.ReplacementLines),
                    $"{item.Name} can't be taken as a replacement item yet — exchanges only support plain items and item variants so far.");
            }

            var unitPrice = item.BasePrice;
            if (item.PricingType == PricingType.VariantMatrix)
            {
                if (requested.ItemVariantId is not { } variantId)
                {
                    throw new ValidationException(nameof(request.ReplacementLines), $"{item.Name} requires choosing a variant.");
                }

                var variant = await itemVariantRepository.GetByIdAsync(variantId, cancellationToken)
                    ?? throw new NotFoundException("Item variant", variantId);

                if (variant.ItemId != item.Id)
                {
                    throw new ValidationException(nameof(request.ReplacementLines), "This variant does not belong to the specified item.");
                }

                unitPrice = variant.PriceOverride ?? item.BasePrice;
            }

            replacementLines.Add(new AdjustmentReplacementLine
            {
                TenantId = CurrentTenantId,
                ItemId = item.Id,
                ItemVariantId = requested.ItemVariantId,
                Quantity = requested.Quantity,
                UnitPrice = unitPrice,
                LineTotal = unitPrice * requested.Quantity,
            });
        }

        var approver = await approverAuthorizationService.AuthorizeAsync(request.ApproverPin, cancellationToken);

        var returnedTotal = returnLines.Sum(line => line.LineTotal);
        var replacementTotal = replacementLines.Sum(line => line.LineTotal);
        var priceDifference = replacementTotal - returnedTotal;

        decimal? changeGiven = null;
        if (priceDifference != 0)
        {
            if (request.SettlementMethod is not { } settlementMethod || !SupportedSettlementMethods.Contains(settlementMethod))
            {
                throw new ValidationException(
                    nameof(request.SettlementMethod),
                    "A settlement method (Cash, Bank Transfer or Manual GCash QR) is required when the return and replacement totals differ.");
            }

            if (settlementMethod == PaymentMethod.Cash && priceDifference > 0)
            {
                if (request.SettlementAmountTendered is not { } tendered || tendered < priceDifference)
                {
                    throw new ValidationException(nameof(request.SettlementAmountTendered), "Cash tendered must cover the amount owed.");
                }

                changeGiven = tendered - priceDifference;
            }
        }

        var adjustment = new Adjustment
        {
            TenantId = CurrentTenantId,
            OriginalTransactionId = transaction.Id,
            BranchId = transaction.BranchId,
            DeviceId = CurrentDeviceId,
            RequestedByUserId = CurrentUserId,
            ApprovedByUserId = approver.Id,
            Reason = request.Reason,
            ReturnedTotal = returnedTotal,
            ReplacementTotal = replacementTotal,
            PriceDifference = priceDifference,
            SettlementMethod = priceDifference != 0 ? request.SettlementMethod : null,
            SettlementAmountTendered = priceDifference != 0 ? request.SettlementAmountTendered : null,
        };
        adjustmentRepository.Add(adjustment);

        foreach (var line in returnLines)
        {
            line.AdjustmentId = adjustment.Id;
            adjustmentRepository.AddReturnLine(line);

            var item = await itemRepository.GetByIdAsync(line.ItemId, cancellationToken);
            if (item is not null)
            {
                _ = await itemStockService.AdjustAsync(item, line.Quantity, cancellationToken);
                inventoryMovementRepository.Add(new InventoryMovement
                {
                    TenantId = CurrentTenantId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ItemId = item.Id,
                    BranchId = transaction.BranchId,
                    Type = MovementType.ForReturn,
                    Quantity = line.Quantity,
                    StaffUserId = CurrentUserId,
                    Note = $"Exchange — returned against receipt #{transaction.ReceiptNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)"}",
                });
            }
        }

        foreach (var line in replacementLines)
        {
            line.AdjustmentId = adjustment.Id;
            adjustmentRepository.AddReplacementLine(line);

            var item = await itemRepository.GetByIdAsync(line.ItemId, cancellationToken);
            if (item is not null)
            {
                _ = await itemStockService.AdjustAsync(item, -line.Quantity, cancellationToken);
                inventoryMovementRepository.Add(new InventoryMovement
                {
                    TenantId = CurrentTenantId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ItemId = item.Id,
                    BranchId = transaction.BranchId,
                    Type = MovementType.StockOut,
                    Quantity = line.Quantity,
                    StaffUserId = CurrentUserId,
                    Note = $"Exchange — replacement against receipt #{transaction.ReceiptNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)"}",
                });
            }
        }

        auditLogRepository.Add(new AuditLog
        {
            TenantId = CurrentTenantId,
            ActorUserId = CurrentUserId,
            ActionType = AuditActionType.Exchange,
            TargetEntityType = nameof(Transaction),
            TargetEntityId = transaction.Id,
            BeforeStateJson = System.Text.Json.JsonSerializer.Serialize(new { receiptNumber = transaction.ReceiptNumber }),
            AfterStateJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                adjustmentId = adjustment.Id,
                reason = request.Reason,
                returnedTotal,
                replacementTotal,
                priceDifference,
                approvedByUserId = approver.Id,
                approvedByRole = approver.Role.ToString(),
            }),
            ApprovedByUserId = approver.Id,
        });

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(adjustment, transaction, returnLines, replacementLines, changeGiven, cancellationToken);
    }

    public async Task<IReadOnlyList<ReturnableLineDto>> ListReturnableLinesAsync(Guid originalTransactionId, CancellationToken cancellationToken = default)
    {
        var transaction = await transactionRepository.GetByIdAsync(originalTransactionId, cancellationToken);
        if (transaction is null || transaction.TenantId != CurrentTenantId)
        {
            throw new NotFoundException("Transaction", originalTransactionId);
        }

        var alreadyReturned = (await adjustmentRepository.ListReturnLinesByTransactionAsync(transaction.Id, cancellationToken))
            .GroupBy(line => line.OriginalLineId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));

        return [.. (await transactionRepository.ListLinesAsync(transaction.Id, cancellationToken))
            .Select(line => new ReturnableLineDto(line.Id, line.Quantity - alreadyReturned.GetValueOrDefault(line.Id)))];
    }

    private async Task<AdjustmentDto> ToDtoAsync(
        Adjustment adjustment,
        Transaction transaction,
        IReadOnlyList<AdjustmentReturnLine> returnLines,
        IReadOnlyList<AdjustmentReplacementLine> replacementLines,
        decimal? changeGiven,
        CancellationToken cancellationToken)
    {
        var itemIds = returnLines.Select(l => l.ItemId).Concat(replacementLines.Select(l => l.ItemId)).Distinct().ToList();
        var itemsById = (await itemRepository.ListByIdsAsync(itemIds, cancellationToken)).ToDictionary(item => item.Id);
        var approver = await userRepository.FindActorAsync(adjustment.ApprovedByUserId, cancellationToken);

        AdjustmentLineDto ToLineDto(Guid itemId, Guid? variantId, decimal quantity, decimal unitPrice, decimal lineTotal) =>
            new(itemId, itemsById.TryGetValue(itemId, out var item) ? item.Name : "(deleted item)", variantId, quantity, unitPrice, lineTotal);

        return new AdjustmentDto(
            adjustment.Id,
            adjustment.OriginalTransactionId,
            transaction.ReceiptNumber,
            adjustment.CreatedAt,
            adjustment.Reason,
            adjustment.ApprovedByUserId,
            approver?.Name ?? "(former staff)",
            [.. returnLines.Select(l => ToLineDto(l.ItemId, l.ItemVariantId, l.Quantity, l.UnitPrice, l.LineTotal))],
            [.. replacementLines.Select(l => ToLineDto(l.ItemId, l.ItemVariantId, l.Quantity, l.UnitPrice, l.LineTotal))],
            adjustment.ReturnedTotal,
            adjustment.ReplacementTotal,
            adjustment.PriceDifference,
            adjustment.SettlementMethod,
            changeGiven);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("An exchange requires an authenticated tenant context.");

    private Guid CurrentUserId => currentActorProvider.UserId
        ?? throw new InvalidOperationException("An exchange requires an authenticated staff user.");

    private Guid CurrentDeviceId => currentActorProvider.DeviceId
        ?? throw new InvalidOperationException("An exchange requires an authenticated device context.");
}
