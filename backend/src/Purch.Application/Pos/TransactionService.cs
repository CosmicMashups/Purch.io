using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.CreditLedger;
using Purch.Application.Inventory;
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
    IItemBatchRepository itemBatchRepository,
    IItemVariantRepository itemVariantRepository,
    IItemComboComponentRepository comboComponentRepository,
    IItemModifierGroupRepository itemModifierGroupRepository,
    IModifierGroupRepository modifierGroupRepository,
    IPromoCodeRepository promoCodeRepository,
    IBogoPromoRuleRepository bogoPromoRuleRepository,
    IComboPromoRuleRepository comboPromoRuleRepository,
    IItemDiscountPromoRuleRepository itemDiscountPromoRuleRepository,
    ICustomerCreditLedgerRepository creditLedgerRepository,
    ITenantRepository tenantRepository,
    IAuditLogRepository auditLogRepository,
    IUserRepository userRepository,
    IItemRecipeRepository itemRecipeRepository,
    IInventoryItemRepository inventoryItemRepository,
    IInventoryMovementRepository inventoryMovementRepository,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : ITransactionService
{
    private static readonly HashSet<Role> ApproverRoles = [Role.Admin, Role.Manager];

    private static readonly HashSet<PaymentMethod> SupportedPaymentMethods =
    [
        PaymentMethod.Cash,
        PaymentMethod.BankTransfer,
        PaymentMethod.ManualGcashQr,
        PaymentMethod.UtangCredit,
    ];

    /// <summary>RA 9994/RA 10754 Senior Citizen/PWD discount — see ApplySeniorPwdDiscountRequest for the VAT-treatment caveat.</summary>
    private const decimal SeniorPwdDiscountRate = 0.20m;

    /// <summary>How far past the terminal's last recorded number a device-issued receipt number may
    /// jump — wide enough for a long offline stretch, tight enough that a typo or a bad client
    /// can't burn the sequence.</summary>
    private const long MaxReceiptNumberJump = 10_000;

    /// <summary>How far back an offline sale's own timestamp is trusted — long enough for any realistic
    /// offline stretch (the terminal itself stops selling offline well before this), short enough that a
    /// wrong device clock can't rewrite history.</summary>
    private static readonly TimeSpan MaxOfflineSaleAge = TimeSpan.FromDays(7);

    /// <summary>Set for the duration of an offline checkout: the time the sale really happened, stamped
    /// on the sale, its payment and its stock movements instead of the moment the server processed it.
    /// The service is scoped per request, so this never leaks between sales.</summary>
    private DateTimeOffset? saleTimeOverride;

    /// <summary>Set while recording an offline sale whose ringing-up staff member was verified: they, not
    /// whoever happens to be signed in when it syncs, are the sale's staff user.</summary>
    private Guid? staffOverride;


    public async Task<TransactionDto> GetOrCreateOpenCartAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await GetOrCreateOpenTransactionAsync(cancellationToken);
        return await ToDtoAsync(transaction, cancellationToken);
    }

    public async Task<TransactionDto> AddLineAsync(AddTransactionLineRequest request, CancellationToken cancellationToken = default)
    {
        return await ToDtoAsync(await AddLineCoreAsync(request, cancellationToken), cancellationToken);
    }

    private async Task<Transaction> AddLineCoreAsync(AddTransactionLineRequest request, CancellationToken cancellationToken)
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
            var (comboUnitPrice, selections) = await ResolveComboSelectionsAsync(item, request.ComboSelections, cancellationToken);
            var (comboModifierPriceDelta, comboModifierIds) = await ResolveModifierSelectionsAsync(item, request.SelectedModifierIds, cancellationToken);
            var unitPrice = comboUnitPrice + comboModifierPriceDelta;

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

            foreach (var modifierId in comboModifierIds)
            {
                transactionRepository.AddModifierSelection(new TransactionLineModifierSelection
                {
                    TenantId = CurrentTenantId,
                    TransactionLineId = line.Id,
                    ItemModifierId = modifierId,
                });
            }

            // RecalculateTotalAsync re-queries lines from the database, which
            // wouldn't see the line just added above until it's flushed — save
            // first so the total isn't computed one line behind.
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await RecalculateTotalAsync(cart, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return cart;
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

        var (modifierPriceDelta, modifierIds) = await ResolveModifierSelectionsAsync(item, request.SelectedModifierIds, cancellationToken);
        resolvedUnitPrice += modifierPriceDelta;

        var lines = await transactionRepository.ListLinesAsync(cart.Id, cancellationToken);

        // Two lines for the same item/variant only merge into one when neither
        // carries a modifier selection — a "No Ice" latte and a regular one are
        // meaningfully different lines, so merging them would silently drop
        // which cups actually got which modifiers.
        // The same holds in reverse: a plain add must not fold into an existing line that
        // already carries modifiers, or it would be charged that line's modified unit price.
        TransactionLine? existingLine = null;
        if (modifierIds.Count == 0)
        {
            foreach (var candidate in lines.Where(line => line.ItemId == request.ItemId && line.ItemVariantId == request.ItemVariantId))
            {
                if ((await transactionRepository.ListModifierSelectionsAsync(candidate.Id, cancellationToken)).Count == 0)
                {
                    existingLine = candidate;
                    break;
                }
            }
        }
        if (existingLine is not null)
        {
            existingLine.Quantity += request.Quantity;
            existingLine.LineTotal = existingLine.Quantity * existingLine.UnitPrice;
        }
        else
        {
            var line = new TransactionLine
            {
                TenantId = CurrentTenantId,
                TransactionId = cart.Id,
                ItemId = request.ItemId,
                ItemVariantId = request.ItemVariantId,
                Quantity = request.Quantity,
                UnitPrice = resolvedUnitPrice,
                LineTotal = resolvedUnitPrice * request.Quantity,
            };
            transactionRepository.AddLine(line);

            foreach (var modifierId in modifierIds)
            {
                transactionRepository.AddModifierSelection(new TransactionLineModifierSelection
                {
                    TenantId = CurrentTenantId,
                    TransactionLineId = line.Id,
                    ItemModifierId = modifierId,
                });
            }
        }

        // Same flush-before-recalculate ordering as the combo branch above —
        // RecalculateTotalAsync's line query otherwise misses this add/update.
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateTotalAsync(cart, cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return cart;
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

    /// <summary>Validates the caller's chosen modifiers against every modifier
    /// group actually attached to this item: every IsRequired group needs at
    /// least one selection, a group without AllowMultipleSelection needs at
    /// most one, and every selected id must belong to an attached group's
    /// modifiers — then prices the line as the sum of each choice's
    /// PriceDelta, the same "fold into UnitPrice" approach combo substitution
    /// upcharges already use.</summary>
    private async Task<(decimal PriceDeltaTotal, IReadOnlyList<Guid> ModifierIds)> ResolveModifierSelectionsAsync(
        Item item,
        IReadOnlyList<Guid>? selectedModifierIds,
        CancellationToken cancellationToken)
    {
        var attachedGroupIds = await itemModifierGroupRepository.ListGroupIdsForItemAsync(item.Id, cancellationToken);
        if (attachedGroupIds.Count == 0)
        {
            return selectedModifierIds is { Count: > 0 }
                ? throw new ValidationException(
                    nameof(AddTransactionLineRequest.SelectedModifierIds),
                    "This item has no modifier groups to select from.")
                : ((decimal PriceDeltaTotal, IReadOnlyList<Guid> ModifierIds))(0m, []);
        }

        var attachedGroups = (await modifierGroupRepository.ListByTenantWithModifiersAsync(CurrentTenantId, cancellationToken))
            .Where(pair => attachedGroupIds.Contains(pair.Group.Id))
            .ToList();

        var selectedIds = (selectedModifierIds ?? []).ToList();
        var selectedModifiersByGroup = attachedGroups
            .Select(pair => (pair.Group, Selected: pair.Modifiers.Where(m => selectedIds.Contains(m.Id)).ToList()))
            .ToList();

        var recognizedModifierIds = attachedGroups.SelectMany(pair => pair.Modifiers.Select(m => m.Id)).ToHashSet();
        var unrecognized = selectedIds.FirstOrDefault(id => !recognizedModifierIds.Contains(id));
        if (unrecognized != Guid.Empty)
        {
            throw new ValidationException(
                nameof(AddTransactionLineRequest.SelectedModifierIds),
                "One of the selected modifiers doesn't belong to this item.");
        }

        foreach (var (group, selected) in selectedModifiersByGroup)
        {
            if (group.IsRequired && selected.Count == 0)
            {
                throw new ValidationException(
                    nameof(AddTransactionLineRequest.SelectedModifierIds),
                    $"Choose an option for \"{group.Name}\".");
            }

            if (!group.AllowMultipleSelection && selected.Count > 1)
            {
                throw new ValidationException(
                    nameof(AddTransactionLineRequest.SelectedModifierIds),
                    $"Only one option can be chosen for \"{group.Name}\".");
            }
        }

        var priceDeltaTotal = selectedModifiersByGroup.SelectMany(pair => pair.Selected).Sum(m => m.PriceDelta);
        return (priceDeltaTotal, selectedIds);
    }

    public async Task<TransactionDto> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SaleId == Guid.Empty)
        {
            throw new ValidationException(nameof(request.SaleId), "A sale id is required.");
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ValidationException(nameof(request.Lines), "The cart is empty — add an item before checking out.");
        }

        // Idempotent replay: this exact sale already went through (a retry after a lost
        // response), so return it as-is instead of building and charging a second one.
        var existing = await transactionRepository.GetByClientSaleIdAsync(request.SaleId, cancellationToken);
        if (existing is not null)
        {
            if (existing.Status == TransactionStatus.Completed)
            {
                return await ToDtoAsync(existing, cancellationToken);
            }

            if (existing.DeviceId != CurrentDeviceId)
            {
                throw new ConflictException("This sale id was already used by another terminal.");
            }

            // A previous attempt stopped before payment (validation error, price change, crash).
            // Discard it and release the id so this attempt can claim it.
            existing.Status = TransactionStatus.Voided;
            existing.ClientSaleId = null;
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Discard any other unpaid cart this device is still holding (e.g. left by an attempt that
        // failed midway, or by the older server-side cart flow) so the sale is built on a clean
        // cart — but never a claimed kiosk order, which is someone's real, pending order.
        var open = await transactionRepository.GetOpenByDeviceAsync(CurrentDeviceId, cancellationToken);
        if (open is not null)
        {
            if (open.OriginatedFromKiosk)
            {
                throw new ConflictException("Finish or void the claimed kiosk order before starting a new sale.");
            }

            // Starting a sale discards whatever unpaid cart the device was holding. That is fine for an empty
            // one, but a cart with items being thrown away by whoever happens to check out next should leave
            // a trace: the manual void of a cart is supervisor-only and audited, and this must not be a way
            // around it.
            if (open.TotalAmount > 0)
            {
                AuditVoid(open, "Discarded when a new sale was checked out");
            }

            open.Status = TransactionStatus.Voided;
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (request.OfflineSale && request.RungByStaffId is { } rungByStaffId)
        {
            // Tenant-scoped lookup, so an id from another tenant simply isn't found. The role comes from
            // the database, never from the request.
            var rungBy = await userRepository.GetByIdAsync(rungByStaffId, cancellationToken);
            if (request.SeniorPwdDiscountApplied && rungBy?.Role is not (Role.Admin or Role.Manager))
            {
                throw new ForbiddenException("Only a manager or admin can apply the Senior Citizen/PWD discount.");
            }

            staffOverride = rungBy?.Id;
        }

        var cart = await GetOrCreateOpenTransactionAsync(cancellationToken);
        cart.ClientSaleId = request.SaleId;
        if (request.OfflineSale && request.SoldAt is { } soldAt)
        {
            var now = DateTimeOffset.UtcNow;
            saleTimeOverride = soldAt > now ? now : (soldAt < now - MaxOfflineSaleAge ? now - MaxOfflineSaleAge : soldAt);
            cart.CreatedAt = saleTimeOverride.Value;
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        // Deliberately no cleanup in a catch: if anything below throws, this cart is left Open
        // (still holding the SaleId) and the next checkout attempt discards it above, from a
        // fresh unit of work. Cleaning up here would re-save whatever half-applied changes
        // (e.g. a credit-ledger balance) the failed step left in the change tracker.
        foreach (var line in request.Lines)
        {
            _ = await AddLineCoreAsync(line, cancellationToken);
        }

        if (request.SeniorPwdDiscountApplied)
        {
            _ = await ApplySeniorPwdDiscountCoreAsync(new ApplySeniorPwdDiscountRequest(true), cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.PromoCode))
        {
            _ = await ApplyPromoCodeCoreAsync(new ApplyPromoCodeRequest(request.PromoCode), cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.OrderType))
        {
            _ = await SetOrderTypeCoreAsync(new SetOrderTypeRequest(request.OrderType), cancellationToken);
        }

        // An offline sale is already paid for at the device's price — refusing it now would leave the
        // customer with a receipt for a sale the books never see. It is recorded at the server's price.
        return !request.OfflineSale && request.ExpectedTotal is { } expectedTotal && Math.Abs(cart.TotalAmount - expectedTotal) > 0.005m
            ? throw new ConflictException(
                $"Prices or promos changed: the total is now {cart.TotalAmount:F2} (the device showed {expectedTotal:F2}). Review the cart and try again.")
            : await RecordPaymentCoreAsync(request.Payment, request.ReceiptNumber, cancellationToken);
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

        // Flush the quantity/LineTotal change above before recalculating — its
        // line query would otherwise still see the old quantity.
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
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

        AuditVoid(cart, "Voided by staff");
        cart.Status = TransactionStatus.Voided;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    /// <summary>Records that a cart with content was voided and by whom, staged so it commits in the same
    /// save as the void. Call before changing the cart's status, which the entry captures.</summary>
    private void AuditVoid(Transaction cart, string reason)
    {
        auditLogRepository.Add(new AuditLog
        {
            TenantId = CurrentTenantId,
            ActorUserId = CurrentUserId,
            ActionType = AuditActionType.Void,
            TargetEntityType = nameof(Transaction),
            TargetEntityId = cart.Id,
            BeforeStateJson = JsonSerializer.Serialize(new { status = cart.Status.ToString(), total = cart.TotalAmount }),
            AfterStateJson = JsonSerializer.Serialize(new { status = nameof(TransactionStatus.Voided), reason }),
        });
    }

    public async Task<TransactionDto> RefundTransactionAsync(Guid transactionId, RefundTransactionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ValidationException(nameof(request.Reason), "Refund reason is required.");
        }

        var transaction = await transactionRepository.GetByIdAsync(transactionId, cancellationToken)
            ?? throw new NotFoundException("Transaction", transactionId);

        if (transaction.TenantId != CurrentTenantId)
        {
            throw new NotFoundException("Transaction", transactionId);
        }

        if (transaction.Status != TransactionStatus.Completed)
        {
            throw new ValidationException(nameof(transaction.Status), "Only completed transactions can be refunded.");
        }

        var beforeState = new { status = transaction.Status.ToString(), total = transaction.TotalAmount };
        transaction.Status = TransactionStatus.Refunded;

        auditLogRepository.Add(new AuditLog
        {
            TenantId = CurrentTenantId,
            ActorUserId = CurrentUserId,
            ActionType = AuditActionType.Refund,
            TargetEntityType = nameof(Transaction),
            TargetEntityId = transaction.Id,
            BeforeStateJson = JsonSerializer.Serialize(beforeState),
            AfterStateJson = JsonSerializer.Serialize(new { status = nameof(TransactionStatus.Refunded), reason = request.Reason, total = transaction.TotalAmount }),
        });

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(transaction, cancellationToken);
    }

    public async Task<long> GetLastIssuedReceiptNumberAsync(CancellationToken cancellationToken = default)
    {
        var sequence = await receiptSequenceRepository.GetOrCreateTrackedAsync(CurrentTenantId, CurrentBranchId, CurrentDeviceId, cancellationToken);
        return sequence.LastIssuedNumber;
    }

    public Task<TransactionDto> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default)
    {
        return RecordPaymentCoreAsync(request, deviceIssuedReceiptNumber: null, cancellationToken);
    }

    private async Task<TransactionDto> RecordPaymentCoreAsync(RecordPaymentRequest request, long? deviceIssuedReceiptNumber, CancellationToken cancellationToken)
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
            await ChargeToCreditLedgerAsync(cart, request, cancellationToken);
        }

        paymentRepository.Add(new Payment
        {
            TenantId = CurrentTenantId,
            CreatedAt = saleTimeOverride ?? DateTimeOffset.UtcNow,
            TransactionId = cart.Id,
            Method = request.Method,
            Status = PaymentStatus.Confirmed,
            Amount = cart.TotalAmount,
            AmountTendered = request.AmountTendered,
            ChangeGiven = changeGiven,
        });

        var sequence = await receiptSequenceRepository.GetOrCreateTrackedAsync(CurrentTenantId, cart.BranchId, cart.DeviceId, cancellationToken);
        if (deviceIssuedReceiptNumber is { } issuedNumber)
        {
            // The terminal numbered this sale itself (so it could print before the server saw it).
            // Numbers are unique per terminal and never reused; the high-water mark moves up to
            // the number rather than incrementing, since a device may sync slightly out of order.
            if (issuedNumber <= 0 || issuedNumber > sequence.LastIssuedNumber + MaxReceiptNumberJump)
            {
                throw new ValidationException(nameof(CheckoutRequest.ReceiptNumber), "That receipt number is out of range for this terminal.");
            }

            if (await transactionRepository.ReceiptNumberExistsAsync(cart.DeviceId, issuedNumber, cancellationToken))
            {
                throw new ConflictException($"Receipt number {issuedNumber} was already issued to this terminal.");
            }

            sequence.LastIssuedNumber = Math.Max(sequence.LastIssuedNumber, issuedNumber);
            cart.ReceiptNumber = issuedNumber;
        }
        else
        {
            sequence.LastIssuedNumber += 1;
            cart.ReceiptNumber = sequence.LastIssuedNumber;
        }

        cart.Status = TransactionStatus.Completed;

        await DecrementStockForCompletedSaleAsync(cart, cancellationToken);
        await ConsumeInventoryForCompletedSaleAsync(cart, cancellationToken);
        await ConsumeBatchesForCompletedSaleAsync(cart, cancellationToken);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    /// <summary>Deducts each line's quantity from its item's StockOnHand and logs a Sale
    /// InventoryMovement per item, in the same SaveChangesAsync as completing the sale, so a
    /// payment and its stock effect can never land separately — this is the only place a
    /// completed sale touches stock; manual InventoryMovements are for everything else
    /// (deliveries, spoilage, corrections). Skipped for tenants that have opted into
    /// UseSeparateInventoryTracking: for them, stock lives on InventoryItem and is maintained by
    /// ConsumeInventoryForCompletedSaleAsync/ReceiveStockAsync instead — StockOnHand would
    /// otherwise drift negative forever since nothing replenishes it once a tenant switches over.</summary>
    private async Task DecrementStockForCompletedSaleAsync(Transaction cart, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(CurrentTenantId, cancellationToken);
        if (tenant is not null && tenant.UseSeparateInventoryTracking)
        {
            return;
        }

        foreach (var (itemId, quantitySold) in await SoldUnitsAsync(cart, cancellationToken))
        {
            var item = await itemRepository.GetByIdAsync(itemId, cancellationToken);
            if (item is null)
            {
                continue;
            }

            item.StockOnHand -= quantitySold;

            inventoryMovementRepository.Add(new InventoryMovement
            {
                TenantId = CurrentTenantId,
                CreatedAt = saleTimeOverride ?? DateTimeOffset.UtcNow,
                ItemId = item.Id,
                BranchId = cart.BranchId,
                Type = MovementType.Sale,
                Quantity = quantitySold,
                StaffUserId = CurrentUserId,
                Note = $"Sale — receipt #{cart.ReceiptNumber}",
            });
        }
    }

    /// <summary>Weight/volume items are received in batches (lots with an expiry date), so a sale has to
    /// draw the sold quantity down from those batches too — earliest expiry first — or every batch would
    /// keep showing everything it was received with. Item-level stock is handled separately; this only
    /// keeps the per-batch remainder honest. A quantity beyond what the batches hold (overselling) is
    /// simply not attributed to any batch.</summary>
    private async Task ConsumeBatchesForCompletedSaleAsync(Transaction cart, CancellationToken cancellationToken)
    {
        foreach (var (itemId, quantitySold) in await SoldUnitsAsync(cart, cancellationToken))
        {
            var item = await itemRepository.GetByIdAsync(itemId, cancellationToken);
            if (item?.PricingType != PricingType.WeightVolume)
            {
                continue;
            }

            var remaining = quantitySold;
            foreach (var batch in await itemBatchRepository.ListConsumableAsync(itemId, cancellationToken))
            {
                if (remaining <= 0)
                {
                    break;
                }

                var taken = Math.Min(batch.QuantityRemaining, remaining);
                batch.QuantityRemaining -= taken;
                remaining -= taken;
            }
        }
    }

    /// <summary>What a completed cart actually took off the shelves, per item. A combo is not itself
    /// stocked: the customer receives the component items they picked, so those are what leave stock
    /// (one of each per combo sold). A service has no stock to draw down. Everything else is its own
    /// quantity. Both stock paths (Item.StockOnHand and separate InventoryItem tracking) read this.</summary>
    private async Task<IReadOnlyList<(Guid ItemId, decimal Quantity)>> SoldUnitsAsync(Transaction cart, CancellationToken cancellationToken)
    {
        var units = new Dictionary<Guid, decimal>();

        foreach (var line in await transactionRepository.ListLinesAsync(cart.Id, cancellationToken))
        {
            var pricingType = (await itemRepository.GetByIdAsync(line.ItemId, cancellationToken))?.PricingType;
            if (pricingType == PricingType.Service)
            {
                continue;
            }

            if (pricingType == PricingType.Combo)
            {
                foreach (var selection in await transactionRepository.ListComboSelectionsAsync(line.Id, cancellationToken))
                {
                    units[selection.SelectedItemId] = units.GetValueOrDefault(selection.SelectedItemId) + line.Quantity;
                }

                continue;
            }

            units[line.ItemId] = units.GetValueOrDefault(line.ItemId) + line.Quantity;
        }

        return [.. units.Select(unit => (unit.Key, unit.Value))];
    }

    /// <summary>When the tenant has opted into UseSeparateInventoryTracking, decrements each
    /// sold item's recipe ingredients (InventoryItem.QuantityOnHand) by QuantityPerOrder times
    /// the quantity sold, logging a Consumption InventoryMovement per ingredient — in the same
    /// SaveChangesAsync as completing the sale, alongside DecrementStockForCompletedSaleAsync.</summary>
    private async Task ConsumeInventoryForCompletedSaleAsync(Transaction cart, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(CurrentTenantId, cancellationToken);
        if (tenant is null || !tenant.UseSeparateInventoryTracking)
        {
            return;
        }

        foreach (var (itemId, quantitySold) in await SoldUnitsAsync(cart, cancellationToken))
        {
            var recipeLines = await itemRecipeRepository.ListByItemAsync(itemId, cancellationToken);

            // An item with no recipe is stocked through its auto-paired InventoryItem (the same
            // record ItemService reads for IsOutOfStock), so a sale must draw that down directly —
            // otherwise such items never deplete under separate tracking.
            if (recipeLines.Count == 0)
            {
                var linkedInventoryItem = await inventoryItemRepository.GetByLinkedItemIdAsync(CurrentTenantId, itemId, cancellationToken);
                if (linkedInventoryItem is not null)
                {
                    linkedInventoryItem.QuantityOnHand -= quantitySold;

                    inventoryMovementRepository.Add(new InventoryMovement
                    {
                        TenantId = CurrentTenantId,
                        CreatedAt = saleTimeOverride ?? DateTimeOffset.UtcNow,
                        ItemId = itemId,
                        InventoryItemId = linkedInventoryItem.Id,
                        BranchId = cart.BranchId,
                        Type = MovementType.Sale,
                        Quantity = quantitySold,
                        StaffUserId = CurrentUserId,
                        Note = $"Sale — receipt #{cart.ReceiptNumber}",
                    });
                }

                continue;
            }

            foreach (var recipeLine in recipeLines)
            {
                if (recipeLine.QuantityPerOrder is not { } quantityPerOrder)
                {
                    continue;
                }

                var inventoryItem = await inventoryItemRepository.GetByIdAsync(recipeLine.InventoryItemId, cancellationToken);
                if (inventoryItem is null)
                {
                    continue;
                }

                var consumedQuantity = quantityPerOrder * quantitySold;
                inventoryItem.QuantityOnHand -= consumedQuantity;

                inventoryMovementRepository.Add(new InventoryMovement
                {
                    TenantId = CurrentTenantId,
                    CreatedAt = saleTimeOverride ?? DateTimeOffset.UtcNow,
                    ItemId = itemId,
                    InventoryItemId = inventoryItem.Id,
                    BranchId = cart.BranchId,
                    Type = MovementType.Consumption,
                    Quantity = consumedQuantity,
                    StaffUserId = CurrentUserId,
                    Note = $"Consumed for sale of item {itemId}",
                });
            }
        }
    }

    /// <summary>B7's checkout-side enforcement: utang is off unless the tenant
    /// has explicitly enabled it, the named customer account must exist and
    /// still be active, and the sale can't push that account's balance past
    /// its credit limit. On success, the ledger's Balance is updated and a
    /// CreditTransaction recorded in the same SaveChangesAsync as the payment
    /// and transaction-completion below, so a rollback can't charge a customer
    /// without actually completing the sale (or vice versa).</summary>
    private async Task ChargeToCreditLedgerAsync(Transaction cart, RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerCreditLedgerId is not { } ledgerId)
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
            if (!request.AllowCreditLimitOverride)
            {
                throw new ValidationException(nameof(RecordPaymentRequest.CustomerCreditLedgerId), "This sale would exceed the customer's credit limit.");
            }

            var caller = await userRepository.GetByIdAsync(CurrentUserId, cancellationToken);
            if (caller is null || !ApproverRoles.Contains(caller.Role))
            {
                throw new ForbiddenException("Only a manager or admin can override a customer's credit limit.");
            }

            auditLogRepository.Add(new AuditLog
            {
                TenantId = CurrentTenantId,
                ActorUserId = CurrentUserId,
                ActionType = AuditActionType.CreditLimitOverride,
                TargetEntityType = nameof(CustomerCreditLedger),
                TargetEntityId = ledger.Id,
                BeforeStateJson = JsonSerializer.Serialize(new { creditLimit = ledger.CreditLimit, balance = ledger.Balance }),
                AfterStateJson = JsonSerializer.Serialize(new { newBalance = ledger.Balance + cart.TotalAmount, saleId = cart.Id, reason = request.CreditLimitOverrideReason ?? "Manager approved credit limit override at checkout" }),
            });
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
        return await ToDtoAsync(await ApplySeniorPwdDiscountCoreAsync(request, cancellationToken), cancellationToken);
    }

    private async Task<Transaction> ApplySeniorPwdDiscountCoreAsync(ApplySeniorPwdDiscountRequest request, CancellationToken cancellationToken)
    {
        var deviceId = CurrentDeviceId;
        var cart = await transactionRepository.GetOpenByDeviceAsync(deviceId, cancellationToken)
            ?? throw new NotFoundException("Open cart", deviceId);

        if (cart.SeniorPwdDiscountApplied != request.Apply)
        {
            auditLogRepository.Add(new AuditLog
            {
                TenantId = CurrentTenantId,
                ActorUserId = CurrentUserId,
                ActionType = AuditActionType.DiscountOverride,
                TargetEntityType = nameof(Transaction),
                TargetEntityId = cart.Id,
                BeforeStateJson = JsonSerializer.Serialize(new { seniorPwdDiscountApplied = cart.SeniorPwdDiscountApplied, total = cart.TotalAmount }),
                AfterStateJson = JsonSerializer.Serialize(new { seniorPwdDiscountApplied = request.Apply }),
            });
        }

        cart.SeniorPwdDiscountApplied = request.Apply;
        await RecalculateTotalAsync(cart, cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return cart;
    }

    public async Task<TransactionDto> ApplyPromoCodeAsync(ApplyPromoCodeRequest request, CancellationToken cancellationToken = default)
    {
        return await ToDtoAsync(await ApplyPromoCodeCoreAsync(request, cancellationToken), cancellationToken);
    }

    private async Task<Transaction> ApplyPromoCodeCoreAsync(ApplyPromoCodeRequest request, CancellationToken cancellationToken)
    {
        // Auto-creates the cart like AddLineAsync — a cashier can key in a promo
        // code before scanning the first item, so requiring an existing open
        // cart here would reject that as a 404 instead of validating the code.
        var cart = await GetOrCreateOpenTransactionAsync(cancellationToken);

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

        return cart;
    }

    public async Task<TransactionDto> SetOrderTypeAsync(SetOrderTypeRequest request, CancellationToken cancellationToken = default)
    {
        return await ToDtoAsync(await SetOrderTypeCoreAsync(request, cancellationToken), cancellationToken);
    }

    private async Task<Transaction> SetOrderTypeCoreAsync(SetOrderTypeRequest request, CancellationToken cancellationToken)
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

        return cart;
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

    public async Task<TransactionDto> UpdateKitchenStatusAsync(Guid transactionId, UpdateKitchenStatusRequest request, CancellationToken cancellationToken = default)
    {
        var order = await transactionRepository.GetByIdAsync(transactionId, cancellationToken);
        if (order is null
            || order.TenantId != CurrentTenantId
            || order.BranchId != CurrentBranchId
            || !order.OriginatedFromKiosk)
        {
            throw new NotFoundException("Kiosk order", transactionId);
        }

        order.KitchenStatus = request.KitchenStatus;
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
            StaffUserId = staffOverride ?? currentActorProvider.UserId,
            Status = TransactionStatus.Open,
        };

        transactionRepository.Add(transaction);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return transaction;
    }

    /// <summary>
    /// Prices the cart. Discounts do NOT stack: under Philippine rules (RA 9994) the statutory Senior
    /// Citizen/PWD 20% cannot be combined with an establishment's promotional discounts or voucher codes,
    /// and a cart carries only one promotional discount at a time. The cashier chooses which one the
    /// customer gets — via the Senior/PWD switch — so this applies exactly one of:
    ///
    ///  - Senior/PWD chosen: 20% of the regular (pre-promo) subtotal. Every promotion is suppressed.
    ///  - Otherwise: ONE promotion — the automatic item promos (BOGO/combo/item discount) or the promo
    ///    code, whichever gives the larger discount (a tie goes to the item promos).
    ///
    /// A promo code that isn't applied (Senior/PWD chosen, or item promos are larger) stays stored on the
    /// cart with a zero amount, so switching Senior/PWD back off restores it. Everything is recomputed from
    /// scratch on every call, never patched incrementally. Mirrored by the Flutter PricingEngine — keep the
    /// two in step.
    /// </summary>
    private async Task RecalculateTotalAsync(Transaction transaction, CancellationToken cancellationToken, Guid? excludingLineId = null)
    {
        var lines = await transactionRepository.ListLinesAsync(transaction.Id, cancellationToken);
        var pricedLines = lines.Where(line => line.Id != excludingLineId).ToList();

        var grossSubtotal = pricedLines.Sum(line => line.LineTotal);

        // Line-level promo fields are persisted, so reset them first and re-apply only if item promos win.
        foreach (var line in pricedLines)
        {
            line.PromoDiscountAmount = 0m;
            line.AppliedPromoLabel = null;
        }

        var itemPromoResults = await CalculateItemPromosAsync(pricedLines, cancellationToken);
        var itemPromoAmount = itemPromoResults.Sum(result => result.DiscountAmount);
        var promoCodeAmount = await CalculatePromoCodeAmountAsync(transaction, grossSubtotal, cancellationToken);

        var seniorPwdAmount = 0m;
        var appliedItemPromoAmount = 0m;
        var appliedPromoCodeAmount = 0m;

        if (transaction.SeniorPwdDiscountApplied)
        {
            // On the regular price, not on a price already reduced by a promotion.
            seniorPwdAmount = Math.Round(grossSubtotal * SeniorPwdDiscountRate, 2);
        }
        else if (itemPromoAmount >= promoCodeAmount)
        {
            appliedItemPromoAmount = itemPromoAmount;
            var linesById = pricedLines.ToDictionary(line => line.Id);
            foreach (var result in itemPromoResults)
            {
                if (linesById.TryGetValue(result.LineId, out var line))
                {
                    line.PromoDiscountAmount = result.DiscountAmount;
                    line.AppliedPromoLabel = result.Label;
                }
            }
        }
        else
        {
            appliedPromoCodeAmount = promoCodeAmount;
        }

        transaction.ItemPromoDiscountAmount = appliedItemPromoAmount;
        transaction.PromoDiscountAmount = appliedPromoCodeAmount;
        transaction.DiscountAmount = seniorPwdAmount + appliedPromoCodeAmount;
        transaction.TotalAmount = grossSubtotal - appliedItemPromoAmount - transaction.DiscountAmount;
    }

    /// <summary>The discount the cart's promo code WOULD give on the regular subtotal (0 if there is no code).
    /// A code that became invalid or expired mid-cart is dropped rather than erroring on every line edit.</summary>
    private async Task<decimal> CalculatePromoCodeAmountAsync(Transaction transaction, decimal grossSubtotal, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transaction.PromoCode))
        {
            return 0m;
        }

        var promo = await promoCodeRepository.GetByCodeAsync(CurrentTenantId, transaction.PromoCode, cancellationToken);
        if (promo is null || !promo.IsActive || (promo.ExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow))
        {
            transaction.PromoCode = null;
            return 0m;
        }

        var amount = promo.DiscountType == PromoDiscountType.Percentage
            ? Math.Round(grossSubtotal * promo.DiscountValue / 100m, 2)
            : promo.DiscountValue;
        return Math.Min(amount, grossSubtotal);
    }

    /// <summary>
    /// The discount the automatic, no-code item-level promos (BOGO, combo bundle, item discount) WOULD give
    /// on these lines, per line. Pure — it changes nothing; RecalculateTotalAsync decides whether these are
    /// the promotion that applies. The heavy lifting (which units get discounted, and by how much, without a
    /// unit being discounted twice by two different rules) lives in ItemPromoPricingCalculator so it can be
    /// unit tested directly.
    /// </summary>
    private async Task<IReadOnlyList<ItemPromoLineResult>> CalculateItemPromosAsync(List<TransactionLine> lines, CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
        {
            return [];
        }

        var now = DateTimeOffset.UtcNow;
        var bogoRules = await bogoPromoRuleRepository.ListActiveByTenantAsync(CurrentTenantId, now, cancellationToken);
        var comboRules = await comboPromoRuleRepository.ListActiveByTenantAsync(CurrentTenantId, now, cancellationToken);
        var itemDiscountRules = await itemDiscountPromoRuleRepository.ListActiveByTenantAsync(CurrentTenantId, now, cancellationToken);

        if (bogoRules.Count == 0 && comboRules.Count == 0 && itemDiscountRules.Count == 0)
        {
            return [];
        }

        var lineInputs = lines
            .Select(line => new ItemPromoPricingCalculator.LineInput(line.Id, line.ItemId, line.Quantity, line.UnitPrice))
            .ToList();

        return ItemPromoPricingCalculator.Calculate(lineInputs, bogoRules, comboRules, itemDiscountRules);
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

            var itemVariantAttributes = new Dictionary<string, string>();
            if (line.ItemVariantId is { } variantIdForDto)
            {
                var variant = await itemVariantRepository.GetByIdAsync(variantIdForDto, cancellationToken);
                if (variant is not null)
                {
                    itemVariantAttributes = JsonSerializer.Deserialize<Dictionary<string, string>>(variant.VariantAttributesJson)
                        ?? [];
                }
            }

            var modifierSelections = await transactionRepository.ListModifierSelectionsAsync(line.Id, cancellationToken);
            var modifierSelectionDtos = new List<ModifierSelectionDto>();
            foreach (var selection in modifierSelections)
            {
                var modifier = await modifierGroupRepository.GetModifierByIdAsync(selection.ItemModifierId, cancellationToken);
                var group = modifier is null ? null : await modifierGroupRepository.GetByIdAsync(modifier.ModifierGroupId, cancellationToken);
                modifierSelectionDtos.Add(new ModifierSelectionDto(
                    selection.ItemModifierId,
                    modifier?.Name ?? "(removed modifier)",
                    group?.Name ?? "(removed group)",
                    modifier?.PriceDelta ?? 0m));
            }

            lineDtos.Add(new TransactionLineDto(
                line.Id,
                line.ItemId,
                item?.Name ?? "(deleted item)",
                line.ItemVariantId,
                itemVariantAttributes,
                line.Quantity,
                line.UnitPrice,
                line.LineTotal,
                line.PromoDiscountAmount,
                line.AppliedPromoLabel,
                comboSelectionDtos,
                modifierSelectionDtos));
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
            transaction.ItemPromoDiscountAmount,
            transaction.TotalAmount,
            transaction.ReceiptNumber,
            transaction.OrderType,
            transaction.OriginatedFromKiosk,
            transaction.KioskPrepNumber == 0 ? null : transaction.KioskPrepNumber,
            transaction.KitchenStatus,
            paymentDtos);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("The POS requires an authenticated tenant context.");

    private Guid CurrentDeviceId => currentActorProvider.DeviceId
        ?? throw new ForbiddenException("The POS requires an authenticated device context.");

    private Guid CurrentBranchId => currentActorProvider.BranchId
        ?? throw new ForbiddenException("The POS requires an authenticated device's branch.");

    private Guid CurrentUserId => staffOverride ?? currentActorProvider.UserId
        ?? throw new InvalidOperationException("The POS requires an authenticated staff user.");
}
