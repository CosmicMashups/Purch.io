using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.CreditLedger;
using Purch.Application.Onboarding;
using Purch.Application.Promotions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Pos;

/// <summary>
/// The cart engine — starting/mutating a device's single in-progress sale.
/// Pricing/discounts/payment/receipt-numbering are out of scope here; those
/// land with the rest of Phase 4's D5/D6 payment and receipt work.
/// </summary>
public sealed class TransactionService(
    ITransactionRepository transactionRepository,
    IPaymentRepository paymentRepository,
    IReceiptSequenceRepository receiptSequenceRepository,
    IKioskPrepSequenceRepository kioskPrepSequenceRepository,
    IItemRepository itemRepository,
    IItemVariantRepository itemVariantRepository,
    IItemComboComponentRepository comboComponentRepository,
    IPromoCodeRepository promoCodeRepository,
    ICustomerCreditLedgerRepository creditLedgerRepository,
    ITenantRepository tenantRepository,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : ITransactionService
{
    private static readonly HashSet<PaymentMethod> SupportedPaymentMethods =
    [
        PaymentMethod.Cash,
        PaymentMethod.BankTransfer,
        PaymentMethod.ManualGcashQr,
        PaymentMethod.UtangCredit,
    ];

    /// <summary>RA 9994/RA 10754 Senior Citizen/PWD discount — see ApplySeniorPwdDiscountRequest for the VAT-treatment caveat.</summary>
    private const decimal SeniorPwdDiscountRate = 0.20m;


    public async Task<TransactionDto> GetOrCreateOpenCartAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await GetOrCreateOpenTransactionAsync(cancellationToken);
        return await ToDtoAsync(transaction, cancellationToken);
    }

    public async Task<TransactionDto> AddLineAsync(AddTransactionLineRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ValidationException(nameof(request.Quantity), "Quantity must be greater than zero.");
        }

        var item = await itemRepository.GetByIdAsync(request.ItemId, cancellationToken)
            ?? throw new NotFoundException("Item", request.ItemId);

        if (!item.IsActive)
        {
            throw new ValidationException(nameof(request.ItemId), "This item is not active.");
        }

        if (item.PricingType == PricingType.VariantMatrix && request.ItemVariantId is null)
        {
            throw new ValidationException(nameof(request.ItemVariantId), "This item requires choosing a variant.");
        }

        var cart = await GetOrCreateOpenTransactionAsync(cancellationToken);

        if (item.PricingType == PricingType.Combo)
        {
            var (unitPrice, selections) = await ResolveComboSelectionsAsync(item, request.ComboSelections, cancellationToken);

            var line = new TransactionLine
            {
                TenantId = CurrentTenantId,
                TransactionId = cart.Id,
                ItemId = request.ItemId,
                ItemVariantId = null,
                Quantity = request.Quantity,
                UnitPrice = unitPrice,
                LineTotal = unitPrice * request.Quantity,
            };
            transactionRepository.AddLine(line);

            foreach (var selection in selections)
            {
                transactionRepository.AddComboSelection(new TransactionLineComboSelection
                {
                    TenantId = CurrentTenantId,
                    TransactionLineId = line.Id,
                    ItemComboComponentId = selection.SlotId,
                    SelectedItemId = selection.SelectedItemId,
                });
            }

            await RecalculateTotalAsync(cart, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return await ToDtoAsync(cart, cancellationToken);
        }

        var resolvedUnitPrice = item.BasePrice;

        if (request.ItemVariantId is { } variantId)
        {
            var variant = await itemVariantRepository.GetByIdAsync(variantId, cancellationToken)
                ?? throw new NotFoundException("Item variant", variantId);

            if (variant.ItemId != item.Id)
            {
                throw new ValidationException(nameof(request.ItemVariantId), "This variant does not belong to the specified item.");
            }

            resolvedUnitPrice = variant.PriceOverride ?? item.BasePrice;
        }

        var lines = await transactionRepository.ListLinesAsync(cart.Id, cancellationToken);

        var existingLine = lines.FirstOrDefault(line => line.ItemId == request.ItemId && line.ItemVariantId == request.ItemVariantId);
        if (existingLine is not null)
        {
            existingLine.Quantity += request.Quantity;
            existingLine.LineTotal = existingLine.Quantity * existingLine.UnitPrice;
        }
        else
        {
            transactionRepository.AddLine(new TransactionLine
            {
                TenantId = CurrentTenantId,
                TransactionId = cart.Id,
                ItemId = request.ItemId,
                ItemVariantId = request.ItemVariantId,
                Quantity = request.Quantity,
                UnitPrice = resolvedUnitPrice,
                LineTotal = resolvedUnitPrice * request.Quantity,
            });
        }

        await RecalculateTotalAsync(cart, cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    /// <summary>Validates that every combo slot got exactly its required number of
    /// selections, each a real, active item from that slot's category, and prices
    /// the line as the combo's base price plus every selected slot's upcharge (if
    /// any) — the domain model prices a slot's substitution as a flat amount, not
    /// per specific component, so any pick within a paid slot costs the same.</summary>
    private async Task<(decimal UnitPrice, IReadOnlyList<ComboSelectionRequest> Selections)> ResolveComboSelectionsAsync(
        Item item,
        IReadOnlyList<ComboSelectionRequest>? requestedSelections,
        CancellationToken cancellationToken)
    {
        var slots = await comboComponentRepository.ListByItemAsync(item.Id, cancellationToken);
        if (slots.Count == 0)
        {
            throw new ValidationException(nameof(item.Id), "This combo has no configured slots yet.");
        }

        var selections = requestedSelections ?? [];
        var slotIds = slots.Select(slot => slot.Id).ToHashSet();
        if (selections.Any(selection => !slotIds.Contains(selection.SlotId)))
        {
            throw new ValidationException(nameof(AddTransactionLineRequest.ComboSelections), "One of the selections doesn't belong to this combo.");
        }

        foreach (var slot in slots)
        {
            var slotSelections = selections.Where(selection => selection.SlotId == slot.Id).ToList();
            if (slotSelections.Count != slot.Quantity)
            {
                throw new ValidationException(
                    nameof(AddTransactionLineRequest.ComboSelections),
                    $"Choose {slot.Quantity} item(s) for \"{slot.SlotLabel}\".");
            }

            foreach (var selection in slotSelections)
            {
                var selectedItem = await itemRepository.GetByIdAsync(selection.SelectedItemId, cancellationToken)
                    ?? throw new NotFoundException("Item", selection.SelectedItemId);

                if (!selectedItem.IsActive || selectedItem.CategoryId != slot.ComponentCategoryId)
                {
                    throw new ValidationException(
                        nameof(AddTransactionLineRequest.ComboSelections),
                        $"\"{selectedItem.Name}\" isn't a valid choice for \"{slot.SlotLabel}\".");
                }
            }
        }

        var unitPrice = item.BasePrice + slots.Sum(slot => slot.SubstitutionUpchargeAmount ?? 0m);
        return (unitPrice, selections);
    }

    public async Task<TransactionDto> UpdateLineAsync(Guid lineId, UpdateTransactionLineRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ValidationException(nameof(request.Quantity), "Quantity must be greater than zero.");
        }

        var line = await RequireOwnLineAsync(lineId, cancellationToken);

        line.Quantity = request.Quantity;
        line.LineTotal = line.Quantity * line.UnitPrice;

        var cart = await transactionRepository.GetByIdAsync(line.TransactionId, cancellationToken)
            ?? throw new NotFoundException("Transaction", line.TransactionId);
        await RecalculateTotalAsync(cart, cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    public async Task<TransactionDto> RemoveLineAsync(Guid lineId, CancellationToken cancellationToken = default)
    {
        var line = await RequireOwnLineAsync(lineId, cancellationToken);
        var cart = await transactionRepository.GetByIdAsync(line.TransactionId, cancellationToken)
            ?? throw new NotFoundException("Transaction", line.TransactionId);

        transactionRepository.RemoveLine(line);

        await RecalculateTotalAsync(cart, cancellationToken, excludingLineId: line.Id);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    public async Task<TransactionDto> VoidCartAsync(CancellationToken cancellationToken = default)
    {
        var deviceId = CurrentDeviceId;
        var cart = await transactionRepository.GetOpenByDeviceAsync(deviceId, cancellationToken)
            ?? throw new NotFoundException("Open cart", deviceId);

        cart.Status = TransactionStatus.Voided;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    public async Task<TransactionDto> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default)
    {
        if (!SupportedPaymentMethods.Contains(request.Method))
        {
            throw new ValidationException(
                nameof(request.Method),
                $"{request.Method} isn't available for checkout yet — only Cash, Bank Transfer, Manual GCash QR, and Utang/Credit are supported so far.");
        }

        var deviceId = CurrentDeviceId;
        var cart = await transactionRepository.GetOpenByDeviceAsync(deviceId, cancellationToken)
            ?? throw new NotFoundException("Open cart", deviceId);

        if (cart.TotalAmount <= 0)
        {
            throw new ValidationException(nameof(request.Method), "The cart is empty — add an item before recording a payment.");
        }

        decimal? changeGiven = null;
        if (request.Method == PaymentMethod.Cash)
        {
            if (request.AmountTendered is not { } tendered || tendered < cart.TotalAmount)
            {
                throw new ValidationException(nameof(request.AmountTendered), "Cash tendered must cover the total amount.");
            }

            changeGiven = tendered - cart.TotalAmount;
        }

        if (request.Method == PaymentMethod.UtangCredit)
        {
            await ChargeToCreditLedgerAsync(cart, request.CustomerCreditLedgerId, cancellationToken);
        }

        paymentRepository.Add(new Payment
        {
            TenantId = CurrentTenantId,
            TransactionId = cart.Id,
            Method = request.Method,
            Status = PaymentStatus.Confirmed,
            Amount = cart.TotalAmount,
            AmountTendered = request.AmountTendered,
            ChangeGiven = changeGiven,
        });

        var sequence = await receiptSequenceRepository.GetOrCreateTrackedAsync(CurrentTenantId, cart.BranchId, cart.DeviceId, cancellationToken);
        sequence.LastIssuedNumber += 1;
        cart.ReceiptNumber = sequence.LastIssuedNumber;
        cart.Status = TransactionStatus.Completed;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    /// <summary>B7's checkout-side enforcement: utang is off unless the tenant
    /// has explicitly enabled it, the named customer account must exist and
    /// still be active, and the sale can't push that account's balance past
    /// its credit limit. On success, the ledger's Balance is updated and a
    /// CreditTransaction recorded in the same SaveChangesAsync as the payment
    /// and transaction-completion below, so a rollback can't charge a customer
    /// without actually completing the sale (or vice versa).</summary>
    private async Task ChargeToCreditLedgerAsync(Transaction cart, Guid? customerCreditLedgerId, CancellationToken cancellationToken)
    {
        if (customerCreditLedgerId is not { } ledgerId)
        {
            throw new ValidationException(nameof(RecordPaymentRequest.CustomerCreditLedgerId), "A customer credit account is required for Utang/Credit payments.");
        }

        var tenant = await tenantRepository.GetByIdAsync(CurrentTenantId, cancellationToken);
        if (tenant is null || !tenant.CreditLedgerEnabled)
        {
            throw new ValidationException(nameof(RecordPaymentRequest.Method), "Utang/credit sales aren't enabled for this business.");
        }

        var ledger = await creditLedgerRepository.GetByIdAsync(ledgerId, cancellationToken);
        if (ledger is null || ledger.TenantId != CurrentTenantId || !ledger.IsActive)
        {
            throw new NotFoundException("Customer credit account", ledgerId);
        }

        if (ledger.Balance + cart.TotalAmount > ledger.CreditLimit)
        {
            throw new ValidationException(nameof(RecordPaymentRequest.CustomerCreditLedgerId), "This sale would exceed the customer's credit limit.");
        }

        ledger.Balance += cart.TotalAmount;
        creditLedgerRepository.AddTransaction(new CreditTransaction
        {
            TenantId = CurrentTenantId,
            CustomerCreditLedgerId = ledger.Id,
            TransactionId = cart.Id,
            Amount = cart.TotalAmount,
            Note = "POS sale on credit",
        });
    }

    public async Task<TransactionDto> ApplySeniorPwdDiscountAsync(ApplySeniorPwdDiscountRequest request, CancellationToken cancellationToken = default)
    {
        var deviceId = CurrentDeviceId;
        var cart = await transactionRepository.GetOpenByDeviceAsync(deviceId, cancellationToken)
            ?? throw new NotFoundException("Open cart", deviceId);

        cart.SeniorPwdDiscountApplied = request.Apply;
        await RecalculateTotalAsync(cart, cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    public async Task<TransactionDto> ApplyPromoCodeAsync(ApplyPromoCodeRequest request, CancellationToken cancellationToken = default)
    {
        var deviceId = CurrentDeviceId;
        var cart = await transactionRepository.GetOpenByDeviceAsync(deviceId, cancellationToken)
            ?? throw new NotFoundException("Open cart", deviceId);

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            cart.PromoCode = null;
        }
        else
        {
            var promo = await promoCodeRepository.GetByCodeAsync(CurrentTenantId, request.Code.Trim(), cancellationToken);
            if (promo is null || !promo.IsActive || (promo.ExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow))
            {
                throw new ValidationException(nameof(request.Code), "This promo code isn't valid.");
            }

            cart.PromoCode = promo.Code;
        }

        await RecalculateTotalAsync(cart, cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    public async Task<TransactionDto> SetOrderTypeAsync(SetOrderTypeRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.OrderType))
        {
            throw new ValidationException(nameof(request.OrderType), "Order type is required.");
        }

        var deviceId = CurrentDeviceId;
        var cart = await transactionRepository.GetOpenByDeviceAsync(deviceId, cancellationToken)
            ?? throw new NotFoundException("Open cart", deviceId);

        cart.OrderType = request.OrderType.Trim();
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    public async Task<TransactionDto> SubmitKioskOrderAsync(CancellationToken cancellationToken = default)
    {
        var deviceId = CurrentDeviceId;
        var cart = await transactionRepository.GetOpenByDeviceAsync(deviceId, cancellationToken)
            ?? throw new NotFoundException("Open cart", deviceId);

        if (cart.TotalAmount <= 0)
        {
            throw new ValidationException(nameof(cart.TotalAmount), "Add at least one item before submitting your order.");
        }

        cart.OriginatedFromKiosk = true;
        cart.Status = TransactionStatus.AwaitingPayment;

        var sequence = await kioskPrepSequenceRepository.GetOrCreateTrackedAsync(CurrentTenantId, CurrentBranchId, cancellationToken);
        sequence.LastIssuedNumber += 1;
        cart.KioskPrepNumber = sequence.LastIssuedNumber;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionDto>> ListPendingKioskOrdersAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        var pending = await transactionRepository.ListPendingKioskOrdersByBranchAsync(branchId, cancellationToken);
        var dtos = new List<TransactionDto>();
        foreach (var order in pending)
        {
            dtos.Add(await ToDtoAsync(order, cancellationToken));
        }

        return dtos;
    }

    public async Task<TransactionDto> ClaimKioskOrderAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var order = await transactionRepository.GetByIdAsync(transactionId, cancellationToken);
        if (order is null
            || order.TenantId != CurrentTenantId
            || order.BranchId != CurrentBranchId
            || !order.OriginatedFromKiosk
            || order.Status != TransactionStatus.AwaitingPayment)
        {
            throw new NotFoundException("Pending kiosk order", transactionId);
        }

        var deviceId = CurrentDeviceId;
        var existingCart = await transactionRepository.GetOpenByDeviceAsync(deviceId, cancellationToken);
        if (existingCart is not null)
        {
            throw new ValidationException(nameof(transactionId), "Finish or void your current cart before claiming a kiosk order.");
        }

        order.DeviceId = deviceId;
        order.StaffUserId = CurrentUserId;
        order.Status = TransactionStatus.Open;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(order, cancellationToken);
    }

    private async Task<TransactionLine> RequireOwnLineAsync(Guid lineId, CancellationToken cancellationToken)
    {
        var line = await transactionRepository.GetLineAsync(lineId, cancellationToken)
            ?? throw new NotFoundException("Transaction line", lineId);

        var cart = await transactionRepository.GetByIdAsync(line.TransactionId, cancellationToken);
        return cart is null || cart.DeviceId != CurrentDeviceId || cart.Status != TransactionStatus.Open
            ? throw new NotFoundException("Transaction line", lineId)
            : line;
    }

    private async Task<Transaction> GetOrCreateOpenTransactionAsync(CancellationToken cancellationToken)
    {
        var deviceId = CurrentDeviceId;
        var existing = await transactionRepository.GetOpenByDeviceAsync(deviceId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var transaction = new Transaction
        {
            TenantId = CurrentTenantId,
            BranchId = CurrentBranchId,
            DeviceId = deviceId,
            StaffUserId = currentActorProvider.UserId,
            Status = TransactionStatus.Open,
        };

        transactionRepository.Add(transaction);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return transaction;
    }

    private async Task RecalculateTotalAsync(Transaction transaction, CancellationToken cancellationToken, Guid? excludingLineId = null)
    {
        var lines = await transactionRepository.ListLinesAsync(transaction.Id, cancellationToken);
        var subtotal = lines
            .Where(line => line.Id != excludingLineId)
            .Sum(line => line.LineTotal);

        var seniorPwdAmount = transaction.SeniorPwdDiscountApplied
            ? Math.Round(subtotal * SeniorPwdDiscountRate, 2)
            : 0m;

        var promoAmount = 0m;
        if (!string.IsNullOrWhiteSpace(transaction.PromoCode))
        {
            var promo = await promoCodeRepository.GetByCodeAsync(CurrentTenantId, transaction.PromoCode, cancellationToken);
            if (promo is null || !promo.IsActive || (promo.ExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow))
            {
                // The code became invalid/expired mid-cart — drop it rather than erroring on every line edit.
                transaction.PromoCode = null;
            }
            else
            {
                var remainingAfterSenior = Math.Max(subtotal - seniorPwdAmount, 0m);
                promoAmount = promo.DiscountType == PromoDiscountType.Percentage
                    ? Math.Round(subtotal * promo.DiscountValue / 100m, 2)
                    : promo.DiscountValue;
                promoAmount = Math.Min(promoAmount, remainingAfterSenior);
            }
        }

        transaction.PromoDiscountAmount = promoAmount;
        transaction.DiscountAmount = seniorPwdAmount + promoAmount;
        transaction.TotalAmount = subtotal - transaction.DiscountAmount;
    }

    private async Task<TransactionDto> ToDtoAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        var lines = await transactionRepository.ListLinesAsync(transaction.Id, cancellationToken);
        var lineDtos = new List<TransactionLineDto>();

        foreach (var line in lines)
        {
            var item = await itemRepository.GetByIdAsync(line.ItemId, cancellationToken);

            var comboSelectionDtos = new List<ComboSelectionDto>();
            if (item?.PricingType == PricingType.Combo)
            {
                var slots = await comboComponentRepository.ListByItemAsync(line.ItemId, cancellationToken);
                var selections = await transactionRepository.ListComboSelectionsAsync(line.Id, cancellationToken);

                foreach (var selection in selections)
                {
                    var slot = slots.FirstOrDefault(s => s.Id == selection.ItemComboComponentId);
                    var selectedItem = await itemRepository.GetByIdAsync(selection.SelectedItemId, cancellationToken);
                    comboSelectionDtos.Add(new ComboSelectionDto(
                        selection.ItemComboComponentId,
                        slot?.SlotLabel ?? "(removed slot)",
                        selection.SelectedItemId,
                        selectedItem?.Name ?? "(deleted item)"));
                }
            }

            lineDtos.Add(new TransactionLineDto(
                line.Id,
                line.ItemId,
                item?.Name ?? "(deleted item)",
                line.ItemVariantId,
                line.Quantity,
                line.UnitPrice,
                line.LineTotal,
                comboSelectionDtos));
        }

        var subtotal = lineDtos.Sum(line => line.LineTotal);

        var payments = await paymentRepository.ListByTransactionAsync(transaction.Id, cancellationToken);
        var paymentDtos = payments
            .Select(payment => new PaymentDto(payment.Id, payment.Method, payment.Status, payment.Amount, payment.AmountTendered, payment.ChangeGiven))
            .ToList();

        return new TransactionDto(
            transaction.Id,
            transaction.BranchId,
            transaction.DeviceId,
            transaction.Status,
            lineDtos,
            subtotal,
            transaction.DiscountAmount,
            transaction.SeniorPwdDiscountApplied,
            transaction.PromoCode,
            transaction.PromoDiscountAmount,
            transaction.TotalAmount,
            transaction.ReceiptNumber == 0 ? null : transaction.ReceiptNumber,
            transaction.OrderType,
            transaction.OriginatedFromKiosk,
            transaction.KioskPrepNumber == 0 ? null : transaction.KioskPrepNumber,
            paymentDtos);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("The POS requires an authenticated tenant context.");

    private Guid CurrentDeviceId => currentActorProvider.DeviceId
        ?? throw new InvalidOperationException("The POS requires an authenticated device context.");

    private Guid CurrentBranchId => currentActorProvider.BranchId
        ?? throw new InvalidOperationException("The POS requires an authenticated device's branch.");

    private Guid CurrentUserId => currentActorProvider.UserId
        ?? throw new InvalidOperationException("The POS requires an authenticated staff user.");
}
