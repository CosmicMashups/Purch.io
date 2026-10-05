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
    IItemModifierIngredientRepository modifierIngredientRepository,
    ModifierDtoBuilder modifierDtoBuilder,
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
    IPosSettings posSettings,
    IApproverAuthorizationService approverAuthorizationService,
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

    /// <summary>The most lines one batch may carry: enough for a cashier tapping fast, small enough that one
    /// request cannot hold a database connection for long.</summary>
    private const int MaxBatchLines = 50;

    public async Task<TransactionDto> AddLinesBatchAsync(AddLinesBatchRequest request, CancellationToken cancellationToken = default)
    {
        if (request.BatchId == Guid.Empty)
        {
            throw new ValidationException(nameof(request.BatchId), "A batch needs its own id, so a retry can never add its lines twice.");
        }

        if (request.Lines.Count is 0 or > MaxBatchLines)
        {
            throw new ValidationException(nameof(request.Lines), $"A batch must carry between 1 and {MaxBatchLines} lines.");
        }

        var cart = await GetOrCreateOpenTransactionAsync(cancellationToken);

        // A retry after a lost response: the lines are already in the cart, so add nothing and answer with it.
        if (await transactionRepository.BatchReceiptExistsAsync(request.BatchId, cancellationToken))
        {
            return await ToDtoAsync(cart, cancellationToken);
        }

        var stage = await CartStage.LoadAsync(cart, transactionRepository, cancellationToken);
        foreach (var line in request.Lines)
        {
            await StageLineAsync(stage, line, cancellationToken);
        }

        // One recalculation and one save for the whole batch, receipt included, so the batch is all-or-nothing:
        // a request that dies part-way leaves neither lines nor a receipt, and the retry starts clean. Two
        // requests racing with the same id collide on the receipt key and one gets a 409.
        await RecalculateTotalAsync(cart, cancellationToken, knownLines: stage.Lines);
        transactionRepository.AddBatchReceipt(new CartBatchReceipt { Id = request.BatchId, TenantId = CurrentTenantId, TransactionId = cart.Id });
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    private async Task<Transaction> AddLineCoreAsync(AddTransactionLineRequest request, CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateOpenTransactionAsync(cancellationToken);
        var stage = await CartStage.LoadAsync(cart, transactionRepository, cancellationToken);
        await StageLineAsync(stage, request, cancellationToken);

        // Recalculate from the lines already in memory (existing plus the one just staged) and save once,
        // instead of flushing, re-querying the lines and saving a second time.
        await RecalculateTotalAsync(cart, cancellationToken, knownLines: stage.Lines);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return cart;
    }

    /// <summary>A cart's lines held in memory while one or more adds are staged against it, so the adds see each
    /// other (a second plain add of the same item merges into the first) without saving in between.</summary>
    private sealed class CartStage
    {
        private CartStage(Transaction cart, List<TransactionLine> lines, HashSet<Guid> linesWithModifiers)
        {
            Cart = cart;
            Lines = lines;
            LinesWithModifiers = linesWithModifiers;
        }

        public Transaction Cart { get; }

        public List<TransactionLine> Lines { get; }

        /// <summary>Lines that carry at least one modifier selection, including ones staged in this same request.</summary>
        public HashSet<Guid> LinesWithModifiers { get; }

        public static async Task<CartStage> LoadAsync(Transaction cart, ITransactionRepository transactionRepository, CancellationToken cancellationToken)
        {
            var lines = (await transactionRepository.ListLinesAsync(cart.Id, cancellationToken)).ToList();
            var withModifiers = (await transactionRepository.ListModifierSelectionsByLinesAsync(lines.Select(l => l.Id).ToList(), cancellationToken))
                .Select(selection => selection.TransactionLineId)
                .ToHashSet();
            return new CartStage(cart, lines, withModifiers);
        }
    }

    /// <summary>Validates one add and applies it to the staged cart (a new line or a merge into an existing one)
    /// WITHOUT saving or recalculating; the caller does both once for everything it staged.</summary>
    private async Task StageLineAsync(CartStage stage, AddTransactionLineRequest request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
        {
            throw new ValidationException(nameof(request.Quantity), "Quantity must be greater than zero.");
        }

        var item = await itemRepository.GetByIdAsync(request.ItemId, cancellationToken)
            ?? throw new NotFoundException("Item", request.ItemId);

        if (!item.IsActive)
        {
            throw new ValidationException(nameof(request.ItemId), $"{item.Name} is no longer available.");
        }

        if (item.PricingType == PricingType.VariantMatrix && request.ItemVariantId is null)
        {
            throw new ValidationException(nameof(request.ItemVariantId), "This item requires choosing a variant.");
        }

        var cart = stage.Cart;

        if (item.PricingType == PricingType.Combo)
        {
            var (comboUnitPrice, selections) = await ResolveComboSelectionsAsync(item, request.ComboSelections, cancellationToken);
            var comboModifiers = await ResolveModifierSelectionsAsync(item, request.SelectedModifierIds, request.SelectedCategoryItemIds, cancellationToken);
            var unitPrice = comboUnitPrice + comboModifiers.PriceDelta;

            var comboLine = new TransactionLine
            {
                TenantId = CurrentTenantId,
                TransactionId = cart.Id,
                ItemId = request.ItemId,
                ItemVariantId = null,
                Quantity = request.Quantity,
                UnitPrice = unitPrice,
                LineTotal = unitPrice * request.Quantity,
            };
            transactionRepository.AddLine(comboLine);
            stage.Lines.Add(comboLine);

            foreach (var selection in selections)
            {
                transactionRepository.AddComboSelection(new TransactionLineComboSelection
                {
                    TenantId = CurrentTenantId,
                    TransactionLineId = comboLine.Id,
                    ItemComboComponentId = selection.SlotId,
                    SelectedItemId = selection.SelectedItemId,
                });
            }

            AddModifierSelections(stage, comboLine.Id, comboModifiers);

            return;
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

        var modifiers = await ResolveModifierSelectionsAsync(item, request.SelectedModifierIds, request.SelectedCategoryItemIds, cancellationToken);
        resolvedUnitPrice += modifiers.PriceDelta;

        // Two lines for the same item/variant only merge into one when neither
        // carries a modifier selection — a "No Ice" latte and a regular one are
        // meaningfully different lines, so merging them would silently drop
        // which cups actually got which modifiers.
        // The same holds in reverse: a plain add must not fold into an existing line that
        // already carries modifiers, or it would be charged that line's modified unit price.
        TransactionLine? existingLine = null;
        if (!modifiers.HasAny)
        {
            existingLine = stage.Lines.FirstOrDefault(line =>
                line.ItemId == request.ItemId
                && line.ItemVariantId == request.ItemVariantId
                && !stage.LinesWithModifiers.Contains(line.Id));
        }

        if (existingLine is not null)
        {
            existingLine.Quantity += request.Quantity;
            existingLine.LineTotal = existingLine.Quantity * existingLine.UnitPrice;
            return;
        }

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
        stage.Lines.Add(line);

        AddModifierSelections(stage, line.Id, modifiers);
    }

    /// <summary>One chosen option of a line, resolved and priced: modifiers and category items together.</summary>
    private sealed record ResolvedModifiers(decimal PriceDelta, IReadOnlyList<Guid> ModifierIds, IReadOnlyList<ResolvedCategoryItem> CategoryItems)
    {
        public static ResolvedModifiers None { get; } = new(0m, [], []);

        public bool HasAny => ModifierIds.Count > 0 || CategoryItems.Count > 0;
    }

    private sealed record ResolvedCategoryItem(Guid ItemId, Guid ModifierGroupId, decimal Price);

    private void AddModifierSelections(CartStage stage, Guid lineId, ResolvedModifiers resolved)
    {
        foreach (var modifierId in resolved.ModifierIds)
        {
            transactionRepository.AddModifierSelection(new TransactionLineModifierSelection
            {
                TenantId = CurrentTenantId,
                TransactionLineId = lineId,
                ItemModifierId = modifierId,
            });
            _ = stage.LinesWithModifiers.Add(lineId);
        }

        foreach (var categoryItem in resolved.CategoryItems)
        {
            transactionRepository.AddModifierSelection(new TransactionLineModifierSelection
            {
                TenantId = CurrentTenantId,
                TransactionLineId = lineId,
                ItemId = categoryItem.ItemId,
                ModifierGroupId = categoryItem.ModifierGroupId,
                PriceCharged = categoryItem.Price,
            });
            _ = stage.LinesWithModifiers.Add(lineId);
        }
    }

    /// <summary>Validates that every combo slot got exactly its required number of
    /// selections, each a real, active item from that slot's category, and prices
    /// the line as the combo's base price plus each slot's flat upcharge (charged once
    /// per slot) plus the surcharge of every individual choice that carries one.
    /// A fixed slot (one that names a specific item) needs no pick from the customer:
    /// the server fills its selections in, so stock still leaves for those items.</summary>
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

        var resolved = new List<ComboSelectionRequest>();
        var choiceUpcharges = 0m;

        foreach (var slot in slots)
        {
            var slotSelections = selections.Where(selection => selection.SlotId == slot.Id).ToList();

            if (slot.ComponentItemId is { } fixedItemId)
            {
                var fixedItem = await itemRepository.GetByIdAsync(fixedItemId, cancellationToken)
                    ?? throw new NotFoundException("Item", fixedItemId);

                if (!fixedItem.IsActive)
                {
                    throw new ValidationException(nameof(AddTransactionLineRequest.ComboSelections), $"{fixedItem.Name} is no longer available.");
                }

                // A client may echo the fixed picks back, but nothing else is acceptable for this slot.
                if (slotSelections.Any(selection => selection.SelectedItemId != fixedItemId))
                {
                    throw new ValidationException(
                        nameof(AddTransactionLineRequest.ComboSelections),
                        $"\"{slot.SlotLabel}\" is always {fixedItem.Name}.");
                }

                resolved.AddRange(Enumerable.Range(0, slot.Quantity).Select(_ => new ComboSelectionRequest(slot.Id, fixedItemId)));
                continue;
            }

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

                choiceUpcharges += ComboChoiceUpcharges.For(slot, selectedItem.Id);
                resolved.Add(selection);
            }
        }

        var unitPrice = item.BasePrice + slots.Sum(slot => slot.SubstitutionUpchargeAmount ?? 0m) + choiceUpcharges;
        return (unitPrice, resolved);
    }

    /// <summary>Validates the caller's chosen modifiers against every modifier
    /// group actually attached to this item: every IsRequired group needs at
    /// least one selection, a group without AllowMultipleSelection needs at
    /// most one, and every selected id must belong to an attached group's
    /// modifiers — then prices the line as the sum of each choice's
    /// PriceDelta, the same "fold into UnitPrice" approach combo substitution
    /// upcharges already use.</summary>
    private async Task<ResolvedModifiers> ResolveModifierSelectionsAsync(
        Item item,
        IReadOnlyList<Guid>? selectedModifierIds,
        IReadOnlyList<Guid>? selectedCategoryItemIds,
        CancellationToken cancellationToken)
    {
        var attachedGroupIds = await itemModifierGroupRepository.ListGroupIdsForItemAsync(item.Id, cancellationToken);
        if (attachedGroupIds.Count == 0)
        {
            return selectedModifierIds is { Count: > 0 } || selectedCategoryItemIds is { Count: > 0 }
                ? throw new ValidationException(
                    nameof(AddTransactionLineRequest.SelectedModifierIds),
                    "This item has no modifier groups to select from.")
                : ResolvedModifiers.None;
        }

        var attachedGroups = (await modifierGroupRepository.ListByTenantWithModifiersAsync(CurrentTenantId, cancellationToken))
            .Where(pair => attachedGroupIds.Contains(pair.Group.Id))
            .ToList();

        var selectedIds = (selectedModifierIds ?? []).ToList();
        var selectedModifiersByGroup = attachedGroups
            .Select(pair => (pair.Group, Selected: pair.Modifiers.Where(m => selectedIds.Contains(m.Id)).ToList()))
            .ToList();

        // Category-linked groups offer the category's items live (not excluded, at the group's price).
        var categoryItemIds = (selectedCategoryItemIds ?? []).Distinct().ToList();
        var categoryOffersByGroup = new Dictionary<Guid, IReadOnlyList<ModifierCategoryItemDto>>();
        if (attachedGroups.Any(pair => pair.Group.CategoryId is not null))
        {
            foreach (var dto in await modifierDtoBuilder.BuildAsync(attachedGroups.Where(pair => pair.Group.CategoryId is not null), cancellationToken))
            {
                categoryOffersByGroup[dto.Id] = [.. (dto.CategoryItems ?? []).Where(offer => !offer.IsExcluded)];
            }
        }

        var selectedCategoryByGroup = attachedGroups
            .ToDictionary(
                pair => pair.Group.Id,
                pair => (IReadOnlyList<ModifierCategoryItemDto>)[.. categoryOffersByGroup.GetValueOrDefault(pair.Group.Id, []).Where(offer => categoryItemIds.Contains(offer.ItemId))]);

        var recognizedCategoryItemIds = selectedCategoryByGroup.Values.SelectMany(offers => offers.Select(offer => offer.ItemId)).ToHashSet();
        if (categoryItemIds.Any(id => !recognizedCategoryItemIds.Contains(id)))
        {
            throw new ValidationException(
                nameof(AddTransactionLineRequest.SelectedCategoryItemIds),
                "One of the selected items isn't offered for this item.");
        }

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
            var chosenCount = selected.Count + selectedCategoryByGroup[group.Id].Count;

            if (group.IsRequired && chosenCount == 0)
            {
                throw new ValidationException(
                    nameof(AddTransactionLineRequest.SelectedModifierIds),
                    $"Choose an option for \"{group.Name}\".");
            }

            if (!group.AllowMultipleSelection && chosenCount > 1)
            {
                throw new ValidationException(
                    nameof(AddTransactionLineRequest.SelectedModifierIds),
                    $"Only one option can be chosen for \"{group.Name}\".");
            }
        }

        await RejectSoldOutModifiersAsync(selectedModifiersByGroup.SelectMany(pair => pair.Selected).ToList(), cancellationToken);

        var chosenCategoryItems = new List<ResolvedCategoryItem>();
        foreach (var (groupId, offers) in selectedCategoryByGroup)
        {
            foreach (var offer in offers)
            {
                if (offer.IsOutOfStock)
                {
                    throw new ValidationException(
                        nameof(AddTransactionLineRequest.SelectedCategoryItemIds),
                        $"{offer.Name} is sold out.");
                }

                chosenCategoryItems.Add(new ResolvedCategoryItem(offer.ItemId, groupId, offer.Price));
            }
        }

        var priceDeltaTotal = selectedModifiersByGroup.SelectMany(pair => pair.Selected).Sum(m => m.PriceDelta)
            + chosenCategoryItems.Sum(chosen => chosen.Price);
        return new ResolvedModifiers(priceDeltaTotal, selectedIds, chosenCategoryItems);
    }

    /// <summary>A modifier whose ingredients have run out is shown as sold out and can't be chosen. The check
    /// only applies when the business tracks inventory separately; otherwise modifiers have no stock to run out of.</summary>
    private async Task RejectSoldOutModifiersAsync(List<ItemModifier> selected, CancellationToken cancellationToken)
    {
        if (selected.Count == 0)
        {
            return;
        }

        var tenant = await tenantRepository.GetByIdAsync(CurrentTenantId, cancellationToken);
        if (tenant is not { UseSeparateInventoryTracking: true })
        {
            return;
        }

        var ingredients = await modifierIngredientRepository.ListByModifiersAsync([.. selected.Select(m => m.Id)], cancellationToken);
        if (ingredients.Count == 0)
        {
            return;
        }

        var inventoryById = (await inventoryItemRepository.ListByIdsAsync([.. ingredients.Select(i => i.InventoryItemId).Distinct()], cancellationToken))
            .ToDictionary(inventoryItem => inventoryItem.Id);

        foreach (var modifier in selected)
        {
            if (ModifierAvailability.IsOutOfStock(ingredients.Where(i => i.ItemModifierId == modifier.Id), inventoryById))
            {
                throw new ValidationException(
                    nameof(AddTransactionLineRequest.SelectedModifierIds),
                    $"{modifier.Name} is sold out.");
            }
        }
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
                // Automatic, not a staff-initiated void: starting a new sale must never itself be blocked
                // waiting on an approver PIN, so this path carries none.
                AuditVoid(open, "Discarded when a new sale was checked out", approver: null);
            }

            open.Status = TransactionStatus.Voided;
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (request.OfflineSale && request.RungByStaffId is { } rungByStaffId)
        {
            // Tenant-scoped lookup, so an id from another tenant simply isn't found. The role comes from
            // the database, never from the request.
            var rungBy = await userRepository.FindActorAsync(rungByStaffId, cancellationToken);
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
            cart.CompletedAt = saleTimeOverride.Value;
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        // Deliberately no cleanup in a catch: if anything below throws, this cart is left Open
        // (still holding the SaleId) and the next checkout attempt discards it above, from a
        // fresh unit of work. Cleaning up here would re-save whatever half-applied changes
        // (e.g. a credit-ledger balance) the failed step left in the change tracker.
        // Every line is staged in memory and priced and saved once, not one save and one recalculation per line.
        var stage = await CartStage.LoadAsync(cart, transactionRepository, cancellationToken);
        foreach (var line in request.Lines)
        {
            await StageLineAsync(stage, line, cancellationToken);
        }

        await RecalculateTotalAsync(cart, cancellationToken, knownLines: stage.Lines);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

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
        if (!request.OfflineSale)
        {
            EnforceExpectedTotal(cart.TotalAmount, request.ExpectedTotal);
        }

        return await RecordPaymentCoreAsync(request.Payment, request.ReceiptNumber, expectedTotal: null, enforceExpectedTotal: false, cancellationToken);
    }

    /// <summary>
    /// The server's own total is the one charged, so what the customer was shown must be that same amount. The client says
    /// what it showed; a missing figure is refused (unless the grace switch is on) and a different one stops the sale with a
    /// 409 before anything is charged, so nobody pays a total they were never shown.
    /// </summary>
    private void EnforceExpectedTotal(decimal serverTotal, decimal? expectedTotal)
    {
        if (expectedTotal is not { } expected)
        {
            if (posSettings.RequireExpectedTotal)
            {
                throw new ValidationException("ExpectedTotal", "Send the total the customer was shown (ExpectedTotal), so it can be checked against the server's total before anything is charged.");
            }

            return;
        }

        if (Math.Abs(serverTotal - expected) > 0.005m)
        {
            throw new ConflictException(
                $"Prices or promos changed: the total is now {serverTotal:F2} (the device showed {expected:F2}). Review the cart and try again.");
        }
    }

    public async Task<TransactionDto> UpdateLineAsync(Guid lineId, UpdateTransactionLineRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ValidationException(nameof(request.Quantity), "Quantity must be greater than zero.");
        }

        var line = await RequireOwnLineAsync(lineId, cancellationToken);
        var cart = await transactionRepository.GetByIdAsync(line.TransactionId, cancellationToken)
            ?? throw new NotFoundException("Transaction", line.TransactionId);

        var approver = await RequireKitchenEditAllowedAsync(cart, request.ApproverPin, cancellationToken);

        var oldQuantity = line.Quantity;
        line.Quantity = request.Quantity;
        line.LineTotal = line.Quantity * line.UnitPrice;

        if (approver is not null)
        {
            AuditKitchenOrderLineEdit(
                cart,
                line,
                before: new { itemId = line.ItemId, quantity = oldQuantity },
                after: new { itemId = line.ItemId, quantity = request.Quantity, kind = "quantity changed" },
                approver);
        }

        // Flush the quantity/LineTotal change above before recalculating — its
        // line query would otherwise still see the old quantity.
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateTotalAsync(cart, cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    public async Task<TransactionDto> RemoveLineAsync(Guid lineId, string? approverPin, CancellationToken cancellationToken = default)
    {
        var line = await RequireOwnLineAsync(lineId, cancellationToken);
        var cart = await transactionRepository.GetByIdAsync(line.TransactionId, cancellationToken)
            ?? throw new NotFoundException("Transaction", line.TransactionId);

        var approver = await RequireKitchenEditAllowedAsync(cart, approverPin, cancellationToken);

        if (approver is not null)
        {
            AuditKitchenOrderLineEdit(
                cart,
                line,
                before: new { itemId = line.ItemId, quantity = line.Quantity },
                after: new { itemId = line.ItemId, quantity = line.Quantity, kind = "line removed" },
                approver);
        }

        transactionRepository.RemoveLine(line);

        await RecalculateTotalAsync(cart, cancellationToken, excludingLineId: line.Id);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    public async Task<TransactionDto> VoidCartAsync(VoidCartRequest request, CancellationToken cancellationToken = default)
    {
        var deviceId = CurrentDeviceId;
        var cart = await transactionRepository.GetOpenByDeviceAsync(deviceId, cancellationToken)
            ?? throw new NotFoundException("Open cart", deviceId);

        // An empty cart has nothing to lose, so it voids freely — the same as starting a new sale over it
        // elsewhere in this file. A cart with items always needs approval, matching decision G in the plan.
        User? approver = null;
        if (cart.TotalAmount > 0)
        {
            approver = await approverAuthorizationService.AuthorizeAsync(request.ApproverPin, cancellationToken);
        }

        AuditVoid(cart, request.Reason ?? "Voided by staff", approver);
        cart.Status = TransactionStatus.Voided;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    /// <summary>Records that a cart with content was voided and by whom, staged so it commits in the same
    /// save as the void. Call before changing the cart's status, which the entry captures. The approver's
    /// id and role — not just their name in a UI somewhere — go in the entry itself, so an audit review
    /// never depends on a still-existing account to say who signed off.</summary>
    private void AuditVoid(Transaction cart, string reason, User? approver)
    {
        auditLogRepository.Add(new AuditLog
        {
            TenantId = CurrentTenantId,
            ActorUserId = CurrentUserId,
            ActionType = AuditActionType.Void,
            TargetEntityType = nameof(Transaction),
            TargetEntityId = cart.Id,
            BeforeStateJson = JsonSerializer.Serialize(new { status = cart.Status.ToString(), total = cart.TotalAmount }),
            AfterStateJson = JsonSerializer.Serialize(new
            {
                status = nameof(TransactionStatus.Voided),
                reason,
                approvedByUserId = approver?.Id,
                approvedByRole = approver?.Role.ToString(),
            }),
            ApprovedByUserId = approver?.Id,
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

        var approver = await approverAuthorizationService.AuthorizeAsync(request.ApproverPin, cancellationToken);

        var beforeState = new { status = transaction.Status.ToString(), total = transaction.TotalAmount };
        transaction.Status = TransactionStatus.Refunded;
        transaction.RefundedAt = DateTimeOffset.UtcNow;

        auditLogRepository.Add(new AuditLog
        {
            TenantId = CurrentTenantId,
            ActorUserId = CurrentUserId,
            ActionType = AuditActionType.Refund,
            TargetEntityType = nameof(Transaction),
            TargetEntityId = transaction.Id,
            BeforeStateJson = JsonSerializer.Serialize(beforeState),
            AfterStateJson = JsonSerializer.Serialize(new
            {
                status = nameof(TransactionStatus.Refunded),
                reason = request.Reason,
                total = transaction.TotalAmount,
                approvedByUserId = approver.Id,
                approvedByRole = approver.Role.ToString(),
            }),
            ApprovedByUserId = approver.Id,
        });

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(transaction, cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionDto>> FindCompletedByReceiptNumberAsync(long receiptNumber, CancellationToken cancellationToken = default)
    {
        var matches = await transactionRepository.ListCompletedByReceiptNumberAsync(CurrentTenantId, receiptNumber, cancellationToken);
        var dtos = new List<TransactionDto>();
        foreach (var match in matches)
        {
            dtos.Add(await ToDtoAsync(match, cancellationToken));
        }

        return dtos;
    }

    public async Task<long> GetLastIssuedReceiptNumberAsync(CancellationToken cancellationToken = default)
    {
        var sequence = await receiptSequenceRepository.GetOrCreateTrackedAsync(CurrentTenantId, CurrentBranchId, CurrentDeviceId, cancellationToken);
        return sequence.LastIssuedNumber;
    }

    public Task<TransactionDto> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default)
    {
        return RecordPaymentCoreAsync(request, deviceIssuedReceiptNumber: null, request.ExpectedTotal, enforceExpectedTotal: true, cancellationToken);
    }

    private async Task<TransactionDto> RecordPaymentCoreAsync(
        RecordPaymentRequest request,
        long? deviceIssuedReceiptNumber,
        decimal? expectedTotal,
        bool enforceExpectedTotal,
        CancellationToken cancellationToken)
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

        if (enforceExpectedTotal)
        {
            EnforceExpectedTotal(cart.TotalAmount, expectedTotal);
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
        cart.CompletedAt ??= DateTimeOffset.UtcNow;

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

        // An item chosen as an add-on through a category-linked modifier group is handed over too, so it
        // is sold (and its recipe consumed) once per unit of the line it was added to.
        var lines = await transactionRepository.ListLinesAsync(cart.Id, cancellationToken);
        var addOnSelections = (await transactionRepository.ListModifierSelectionsByLinesAsync([.. lines.Select(line => line.Id)], cancellationToken))
            .Where(selection => selection.ItemId is not null);
        foreach (var selection in addOnSelections)
        {
            var addOnItemId = selection.ItemId!.Value;
            if ((await itemRepository.GetByIdAsync(addOnItemId, cancellationToken))?.PricingType == PricingType.Service)
            {
                continue;
            }

            var lineQuantity = lines.First(line => line.Id == selection.TransactionLineId).Quantity;
            units[addOnItemId] = units.GetValueOrDefault(addOnItemId) + lineQuantity;
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

        await ConsumeModifierIngredientsForCompletedSaleAsync(cart, cancellationToken);
    }

    /// <summary>The stock the chosen modifiers used up (Coke Zero's syrup and water, an extra slice of cheese),
    /// on top of what each item's own recipe drew down. Scaled by the line quantity, and a null amount only
    /// checks availability and takes nothing, like a recipe line.</summary>
    private async Task ConsumeModifierIngredientsForCompletedSaleAsync(Transaction cart, CancellationToken cancellationToken)
    {
        var lines = await transactionRepository.ListLinesAsync(cart.Id, cancellationToken);
        if (lines.Count == 0)
        {
            return;
        }

        var selections = await transactionRepository.ListModifierSelectionsByLinesAsync([.. lines.Select(line => line.Id)], cancellationToken);
        if (selections.Count == 0)
        {
            return;
        }

        var ingredients = (await modifierIngredientRepository.ListByModifiersAsync(
                [.. selections.Where(s => s.ItemModifierId is not null).Select(s => s.ItemModifierId!.Value).Distinct()],
                cancellationToken))
            .ToLookup(ingredient => ingredient.ItemModifierId);

        foreach (var selection in selections.Where(s => s.ItemModifierId is not null))
        {
            var line = lines.First(candidate => candidate.Id == selection.TransactionLineId);

            foreach (var ingredient in ingredients[selection.ItemModifierId!.Value])
            {
                if (ingredient.QuantityPerOrder is not { } quantityPerOrder)
                {
                    continue;
                }

                var inventoryItem = await inventoryItemRepository.GetByIdAsync(ingredient.InventoryItemId, cancellationToken);
                if (inventoryItem is null)
                {
                    continue;
                }

                var consumedQuantity = quantityPerOrder * line.Quantity;
                inventoryItem.QuantityOnHand -= consumedQuantity;

                inventoryMovementRepository.Add(new InventoryMovement
                {
                    TenantId = CurrentTenantId,
                    CreatedAt = saleTimeOverride ?? DateTimeOffset.UtcNow,
                    ItemId = line.ItemId,
                    InventoryItemId = inventoryItem.Id,
                    BranchId = cart.BranchId,
                    Type = MovementType.Consumption,
                    Quantity = consumedQuantity,
                    StaffUserId = CurrentUserId,
                    Note = $"Consumed by a chosen modifier on item {line.ItemId}",
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

            var caller = await userRepository.FindActorAsync(CurrentUserId, cancellationToken);
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

    public async Task<TransactionDto> PlaceKioskOrderAsync(PlaceKioskOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OrderId == Guid.Empty)
        {
            throw new ValidationException(nameof(request.OrderId), "An order id is required.");
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ValidationException(nameof(request.Lines), "Add at least one item before submitting your order.");
        }

        if (string.IsNullOrWhiteSpace(request.OrderType))
        {
            throw new ValidationException(nameof(request.OrderType), "Order type is required.");
        }

        var (paymentPreference, discountHint) = ValidateKioskPaymentChoice(request);

        // Idempotent replay: this exact order already went through (a retry after a lost response),
        // so return it as-is instead of placing a second one.
        var existing = await transactionRepository.GetByClientSaleIdAsync(request.OrderId, cancellationToken);
        if (existing is not null)
        {
            if (existing.Status == TransactionStatus.AwaitingPayment)
            {
                return await ToDtoAsync(existing, cancellationToken);
            }

            if (existing.DeviceId != CurrentDeviceId)
            {
                throw new ConflictException("This order id was already used by another terminal.");
            }

            // A previous attempt stopped before submitting (an unavailable item, a crash). Discard it
            // and release the id so this attempt can claim it.
            existing.Status = TransactionStatus.Voided;
            existing.ClientSaleId = null;
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Discard whatever this terminal was holding — an abandoned walk-away cart, or a leftover from
        // the older line-by-line kiosk flow — so the order is built on a clean cart.
        var open = await transactionRepository.GetOpenByDeviceAsync(CurrentDeviceId, cancellationToken);
        if (open is not null)
        {
            open.Status = TransactionStatus.Voided;
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var cart = await GetOrCreateOpenTransactionAsync(cancellationToken);
        cart.ClientSaleId = request.OrderId;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        // Deliberately no cleanup in a catch: if a line fails (an unavailable item), this cart is left
        // Open holding the OrderId, and a retry with the same OrderId discards it above and starts clean.
        var stage = await CartStage.LoadAsync(cart, transactionRepository, cancellationToken);
        foreach (var line in request.Lines)
        {
            await StageLineAsync(stage, line, cancellationToken);
        }

        await RecalculateTotalAsync(cart, cancellationToken, knownLines: stage.Lines);
        cart.OrderType = request.OrderType.Trim();
        cart.KioskPaymentPreference = paymentPreference;
        cart.KioskDiscountHint = discountHint;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await SubmitKioskOrderAsync(cancellationToken);
    }

    /// <summary>Normalises the kiosk's payment preference and discount hint. A hint is only meaningful with
    /// the "discount" preference; sending one without it is rejected rather than silently dropped.</summary>
    private static (string? Preference, string? Hint) ValidateKioskPaymentChoice(PlaceKioskOrderRequest request)
    {
        var preference = string.IsNullOrWhiteSpace(request.PaymentPreference)
            ? null
            : request.PaymentPreference.Trim().ToLowerInvariant();
        var hint = string.IsNullOrWhiteSpace(request.DiscountHint)
            ? null
            : request.DiscountHint.Trim().ToLowerInvariant();

        if (preference is not null && !KioskPaymentPreferences.All.Contains(preference))
        {
            throw new ValidationException(nameof(request.PaymentPreference), "Choose cash, card, e-wallet or discount.");
        }

        if (hint is not null && preference != KioskPaymentPreferences.Discount)
        {
            throw new ValidationException(nameof(request.DiscountHint), "A discount type can only be sent with the discount payment choice.");
        }

        if (hint is not null && !KioskPaymentPreferences.DiscountHints.Contains(hint))
        {
            throw new ValidationException(nameof(request.DiscountHint), "Choose senior, PWD or other.");
        }

        return (preference, hint);
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

    /// <summary>
    /// A line on an ordinary cart may be changed or removed freely. A cart that has been sent to the kitchen
    /// (a claimed kiosk order — <see cref="Transaction.OriginatedFromKiosk"/>) is different: the kitchen's own
    /// status says whether it is still safe to touch.
    ///  - Queued (the kitchen hasn't started it): the edit is allowed, but a Cashier or Warehouse staff
    ///    member needs an Admin or Manager's PIN first — an Admin/Manager themselves doesn't, since they are
    ///    already that approver.
    ///  - Preparing, Ready or PickedUp: refused outright, no PIN changes that — the item is already being
    ///    made or is done, so the fix is a refund or exchange, not editing the order kitchen already has.
    ///
    /// Returns the approver who signed off, so the caller can audit-log the edit — or null when no
    /// approval was needed at all (an ordinary cart, or an Admin/Manager editing their own kitchen order),
    /// in which case there's nothing unusual to log.
    /// </summary>
    private async Task<User?> RequireKitchenEditAllowedAsync(Transaction cart, string? approverPin, CancellationToken cancellationToken)
    {
        if (!cart.OriginatedFromKiosk)
        {
            return null;
        }

        if (cart.KitchenStatus != KitchenStatus.Queued)
        {
            throw new ConflictException(
                "The kitchen has already started or finished this order, so it can no longer be changed here. Refund or exchange the item instead.");
        }

        var caller = await userRepository.FindActorAsync(CurrentUserId, cancellationToken);
        if (caller is not null && ApproverRoles.Contains(caller.Role))
        {
            return null;
        }

        return await approverAuthorizationService.AuthorizeAsync(approverPin, cancellationToken);
    }

    /// <summary>Records a kitchen-order line edit that needed a different Admin/Manager's approval — see
    /// RequireKitchenEditAllowedAsync. Call before applying the change, which the entry's "before" state
    /// captures; "what" changed is the caller's own description of the line and the edit.</summary>
    private void AuditKitchenOrderLineEdit(Transaction cart, TransactionLine line, object before, object after, User approver)
    {
        auditLogRepository.Add(new AuditLog
        {
            TenantId = CurrentTenantId,
            ActorUserId = CurrentUserId,
            ActionType = AuditActionType.KitchenOrderLineEdited,
            TargetEntityType = nameof(TransactionLine),
            TargetEntityId = line.Id,
            BeforeStateJson = JsonSerializer.Serialize(before),
            AfterStateJson = JsonSerializer.Serialize(new
            {
                transactionId = cart.Id,
                kioskPrepNumber = cart.KioskPrepNumber,
                change = after,
                approvedByUserId = approver.Id,
                approvedByRole = approver.Role.ToString(),
            }),
            ApprovedByUserId = approver.Id,
        });
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
    private async Task RecalculateTotalAsync(Transaction transaction, CancellationToken cancellationToken, Guid? excludingLineId = null, IReadOnlyList<TransactionLine>? knownLines = null)
    {
        var lines = knownLines ?? await transactionRepository.ListLinesAsync(transaction.Id, cancellationToken);
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
        var lineIds = lines.Select(line => line.Id).ToList();

        // Each lookup below is one batched query for the whole cart, not one per line: on a remote
        // database every query is a network round trip, so a per-line loop made each add slower the
        // fuller the cart got.
        var comboSelections = await transactionRepository.ListComboSelectionsByLinesAsync(lineIds, cancellationToken);
        var modifierSelections = await transactionRepository.ListModifierSelectionsByLinesAsync(lineIds, cancellationToken);

        var itemIds = lines.Select(line => line.ItemId)
            .Concat(comboSelections.Select(selection => selection.SelectedItemId))
            .Concat(modifierSelections.Where(selection => selection.ItemId is not null).Select(selection => selection.ItemId!.Value))
            .Distinct()
            .ToList();
        var itemsById = (await itemRepository.ListByIdsAsync(itemIds, cancellationToken)).ToDictionary(item => item.Id);

        var variantIds = lines.Where(line => line.ItemVariantId is not null).Select(line => line.ItemVariantId!.Value).Distinct().ToList();
        var variantsById = (await itemVariantRepository.ListByIdsAsync(variantIds, cancellationToken)).ToDictionary(variant => variant.Id);

        var modifiersById = (await modifierGroupRepository.ListModifiersWithGroupsByIdsAsync(
                modifierSelections.Where(selection => selection.ItemModifierId is not null).Select(selection => selection.ItemModifierId!.Value).Distinct().ToList(), cancellationToken))
            .ToDictionary(entry => entry.Modifier.Id);

        var groupNamesById = modifierSelections.Any(selection => selection.ItemId is not null)
            ? (await modifierGroupRepository.ListByTenantWithModifiersAsync(CurrentTenantId, cancellationToken))
                .ToDictionary(pair => pair.Group.Id, pair => pair.Group.Name)
            : [];

        var slotsByItem = new Dictionary<Guid, IReadOnlyList<ItemComboComponent>>();
        foreach (var comboItemId in lines
            .Where(line => itemsById.TryGetValue(line.ItemId, out var comboItem) && comboItem.PricingType == PricingType.Combo)
            .Select(line => line.ItemId)
            .Distinct())
        {
            slotsByItem[comboItemId] = await comboComponentRepository.ListByItemAsync(comboItemId, cancellationToken);
        }

        var comboSelectionsByLine = comboSelections.ToLookup(selection => selection.TransactionLineId);
        var modifierSelectionsByLine = modifierSelections.ToLookup(selection => selection.TransactionLineId);
        var lineDtos = new List<TransactionLineDto>();

        foreach (var line in lines)
        {
            _ = itemsById.TryGetValue(line.ItemId, out var item);

            var comboSelectionDtos = new List<ComboSelectionDto>();
            if (item?.PricingType == PricingType.Combo)
            {
                var slots = slotsByItem[line.ItemId];

                foreach (var selection in comboSelectionsByLine[line.Id])
                {
                    var slot = slots.FirstOrDefault(s => s.Id == selection.ItemComboComponentId);
                    _ = itemsById.TryGetValue(selection.SelectedItemId, out var selectedItem);
                    comboSelectionDtos.Add(new ComboSelectionDto(
                        selection.ItemComboComponentId,
                        slot?.SlotLabel ?? "(removed slot)",
                        selection.SelectedItemId,
                        selectedItem?.Name ?? "(deleted item)"));
                }
            }

            var itemVariantAttributes = new Dictionary<string, string>();
            if (line.ItemVariantId is { } variantIdForDto && variantsById.TryGetValue(variantIdForDto, out var variant))
            {
                itemVariantAttributes = JsonSerializer.Deserialize<Dictionary<string, string>>(variant.VariantAttributesJson)
                    ?? [];
            }

            var modifierSelectionDtos = new List<ModifierSelectionDto>();
            foreach (var selection in modifierSelectionsByLine[line.Id])
            {
                if (selection.ItemId is { } addOnItemId)
                {
                    modifierSelectionDtos.Add(new ModifierSelectionDto(
                        null,
                        itemsById.TryGetValue(addOnItemId, out var addOnItem) ? addOnItem.Name : "(deleted item)",
                        selection.ModifierGroupId is { } addOnGroupId && groupNamesById.TryGetValue(addOnGroupId, out var addOnGroupName) ? addOnGroupName : "(removed group)",
                        selection.PriceCharged ?? 0m,
                        addOnItemId));
                    continue;
                }

                var found = modifiersById.TryGetValue(selection.ItemModifierId!.Value, out var entry);
                modifierSelectionDtos.Add(new ModifierSelectionDto(
                    selection.ItemModifierId,
                    found ? entry.Modifier.Name : "(removed modifier)",
                    found && entry.Group is not null ? entry.Group.Name : "(removed group)",
                    found ? entry.Modifier.PriceDelta : 0m));
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
            paymentDtos,
            transaction.CreatedAt,
            transaction.CompletedAt,
            transaction.KioskPaymentPreference,
            transaction.KioskDiscountHint);
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
