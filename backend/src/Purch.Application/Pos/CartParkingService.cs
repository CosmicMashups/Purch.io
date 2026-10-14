using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Pos;

/// <summary>
/// Parks a cart (sets it aside mid-sale), lists, resumes, discards and expires parked carts. A parked cart is a
/// Transaction with Status Parked: it keeps its lines and DeviceId but is not an "open cart" to anyone, so it
/// never counts as a sale, never blocks the device's one-open-cart index, and is never voided by a checkout.
/// Resume swaps it back in under the optimistic row-version token on Transaction, so two registers resuming the
/// same cart at once cannot both win (the loser gets a 409).
/// </summary>
public sealed class CartParkingService(
    ITransactionRepository transactionRepository,
    ITransactionService transactionService,
    IItemRepository itemRepository,
    IItemVariantRepository itemVariantRepository,
    IAuditLogRepository auditLogRepository,
    IUserRepository userRepository,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IApproverAuthorizationService approverAuthorizationService,
    IModifierGroupRepository modifierGroupRepository,
    IUnitOfWork unitOfWork) : ICartParkingService
{
    public const string PriceChanged = "PriceChanged";
    public const string Unavailable = "Unavailable";
    public const string NotRepriced = "NotRepriced";

    public Task ParkAsync(string? label, CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExecuteInTransactionAsync<bool>(async ct =>
        {
            var cart = await transactionRepository.GetOpenByDeviceAsync(CurrentDeviceId, ct)
                ?? throw new ValidationException("cart", "There is no cart to park.");

            if (cart.OriginatedFromKiosk)
            {
                throw new ValidationException("cart", "A claimed kiosk order cannot be parked: finish or void it.");
            }

            var lines = await transactionRepository.ListLinesAsync(cart.Id, ct);
            if (lines.Count == 0)
            {
                throw new ValidationException("cart", "An empty cart cannot be parked.");
            }

            SetAside(cart, string.IsNullOrWhiteSpace(label) ? null : label.Trim());
            _ = await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ParkedCartSummaryDto>> ListParkedAsync(CancellationToken cancellationToken = default)
    {
        var parked = await transactionRepository.ListParkedByBranchAsync(CurrentBranchId, cancellationToken);
        if (parked.Count == 0)
        {
            return [];
        }

        var lines = await transactionRepository.ListLinesByTransactionsAsync([.. parked.Select(t => t.Id)], cancellationToken);
        var lineCounts = lines.GroupBy(line => line.TransactionId).ToDictionary(group => group.Key, group => group.Count());

        var names = new Dictionary<Guid, string?>();
        foreach (var userId in parked.Where(t => t.ParkedByUserId is not null).Select(t => t.ParkedByUserId!.Value).Distinct())
        {
            names[userId] = (await userRepository.FindActorAsync(userId, cancellationToken))?.Name;
        }

        return [.. parked.Select(t => new ParkedCartSummaryDto(
            t.Id,
            t.ParkedLabel,
            t.ParkedAt ?? t.CreatedAt,
            t.ParkedByUserId is { } by && names.TryGetValue(by, out var name) ? name : null,
            lineCounts.GetValueOrDefault(t.Id),
            t.TotalAmount))];
    }

    public Task<TransactionDto> ResumeAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExecuteInTransactionAsync(ct => ResumeCoreAsync(transactionId, ct), cancellationToken);
    }

    private async Task<TransactionDto> ResumeCoreAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        var deviceId = CurrentDeviceId;
        var cart = await RequireParkedAsync(transactionId, cancellationToken);

        // Whoever else is mid-sale on this device: an empty cart is thrown away, a cart with items is parked in
        // its place (a swap), a claimed kiosk order is never touched. Saved before the resumed cart turns Open so
        // the one-open-cart-per-device index is never violated.
        var open = await transactionRepository.GetOpenByDeviceAsync(deviceId, cancellationToken);
        if (open is not null)
        {
            if (open.OriginatedFromKiosk)
            {
                throw new ConflictException("Finish or void the claimed kiosk order before resuming a parked cart.");
            }

            var openLines = await transactionRepository.ListLinesAsync(open.Id, cancellationToken);
            if (openLines.Count == 0)
            {
                open.Status = TransactionStatus.Voided;
            }
            else
            {
                SetAside(open, label: null);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Prices and availability may have moved while the cart sat there: reconcile before it is rung up.
        var changed = await ReconcileLinesAsync(cart, cancellationToken);

        auditLogRepository.Add(new AuditLog
        {
            TenantId = CurrentTenantId,
            ActorUserId = CurrentUserId,
            ActionType = AuditActionType.CartResumed,
            TargetEntityType = nameof(Transaction),
            TargetEntityId = cart.Id,
            BeforeStateJson = JsonSerializer.Serialize(new { status = nameof(TransactionStatus.Parked), label = cart.ParkedLabel, parkedAt = cart.ParkedAt }),
            AfterStateJson = JsonSerializer.Serialize(new { status = nameof(TransactionStatus.Open), deviceId, changedLines = changed.Count }),
        });

        cart.DeviceId = deviceId;
        cart.StaffUserId = CurrentUserId;
        cart.Status = TransactionStatus.Open;
        cart.ParkedLabel = null;
        cart.ParkedAt = null;
        cart.ParkedByUserId = null;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var priced = await transactionService.RecalculateCartAsync(cart.Id, cancellationToken);
        return changed.Count == 0 ? priced : priced with { ChangedLines = changed };
    }

    public async Task DiscardAsync(Guid transactionId, string? approverPin, CancellationToken cancellationToken = default)
    {
        // The PIN is checked BEFORE the transaction opens: a wrong PIN records a failed attempt (and a lockout
        // after five), and that write must survive the ValidationException that follows. Inside the transaction
        // it would be rolled back and the lockout would never accumulate on this path.
        User? approver = null;
        var preview = await RequireParkedAsync(transactionId, cancellationToken);
        if (preview.TotalAmount > 0)
        {
            approver = await approverAuthorizationService.AuthorizeAsync(approverPin, cancellationToken);
        }

        _ = await unitOfWork.ExecuteInTransactionAsync<bool>(async ct =>
        {
            // Re-read after the PIN step: the cart may have been resumed, swept or changed meanwhile.
            var cart = await RequireParkedAsync(transactionId, ct);
            if (cart.TotalAmount > 0 && approver is null)
            {
                throw new ValidationException("approverPin", "A manager's PIN is required to discard a cart with items.");
            }

            if (approver is not null)
            {
                auditLogRepository.Add(new AuditLog
                {
                    TenantId = cart.TenantId,
                    ActorUserId = CurrentUserId,
                    ActionType = AuditActionType.Void,
                    TargetEntityType = nameof(Transaction),
                    TargetEntityId = cart.Id,
                    BeforeStateJson = JsonSerializer.Serialize(new { status = nameof(TransactionStatus.Parked), total = cart.TotalAmount }),
                    AfterStateJson = JsonSerializer.Serialize(new
                    {
                        status = nameof(TransactionStatus.Voided),
                        reason = "Parked cart discarded",
                        approvedByUserId = approver.Id,
                        approvedByRole = approver.Role.ToString(),
                    }),
                    ApprovedByUserId = approver.Id,
                });
            }

            auditLogRepository.Add(ExpiredAudit(cart.TenantId, CurrentUserId, cart.Id, cart.TotalAmount, "Discarded"));
            // Cleared so this void counts in the X/Z voided totals (only sweep-expired carts keep ParkedAt).
            cart.ParkedAt = null;
            cart.ParkedLabel = null;
            cart.Status = TransactionStatus.Voided;
            _ = await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }

    public async Task<int> ExpireStaleAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var expired = 0;
        foreach (var parked in await transactionRepository.ListParkedForExpiryAsync(now, cancellationToken))
        {
            // TtlHours <= 0 means the tenant opted out of expiry.
            if (parked.TtlHours <= 0 || parked.ParkedAt is not { } parkedAt || parkedAt.AddHours(parked.TtlHours) > now)
            {
                continue;
            }

            var voided = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                // One conditional UPDATE claims the cart: if a resume/discard got there first this is false and
                // nothing is audited, so there is never a double void or an audit row for a cart that lived on.
                if (!await transactionRepository.TryVoidParkedAsync(parked.Id, ct))
                {
                    return false;
                }

                auditLogRepository.Add(ExpiredAudit(parked.TenantId, Guid.Empty, parked.Id, parked.TotalAmount, "Expired"));
                _ = await unitOfWork.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);

            if (voided)
            {
                expired++;
            }
        }

        return expired;
    }

    private async Task<Transaction> RequireParkedAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        var cart = await transactionRepository.GetByIdAsync(transactionId, cancellationToken);
        if (cart is null || cart.TenantId != CurrentTenantId)
        {
            throw new NotFoundException("Parked cart", transactionId);
        }

        if (cart.BranchId != CurrentBranchId)
        {
            throw new ForbiddenException("This cart was parked at another branch.");
        }

        // Another register may have resumed, discarded or the sweep expired it between the list and this call.
        if (cart.Status != TransactionStatus.Parked)
        {
            throw new ConflictException("This cart is no longer parked.");
        }

        return cart;
    }

    private void SetAside(Transaction cart, string? label)
    {
        auditLogRepository.Add(new AuditLog
        {
            TenantId = CurrentTenantId,
            ActorUserId = CurrentUserId,
            ActionType = AuditActionType.CartParked,
            TargetEntityType = nameof(Transaction),
            TargetEntityId = cart.Id,
            BeforeStateJson = JsonSerializer.Serialize(new { status = cart.Status.ToString(), total = cart.TotalAmount }),
            AfterStateJson = JsonSerializer.Serialize(new { status = nameof(TransactionStatus.Parked), label }),
        });

        cart.Status = TransactionStatus.Parked;
        cart.ParkedLabel = label;
        cart.ParkedAt = DateTimeOffset.UtcNow;
        cart.ParkedByUserId = CurrentUserId;
    }

    private static AuditLog ExpiredAudit(Guid tenantId, Guid actorUserId, Guid cartId, decimal total, string reason)
    {
        return new AuditLog
        {
            TenantId = tenantId,
            ActorUserId = actorUserId,
            ActionType = AuditActionType.CartExpired,
            TargetEntityType = nameof(Transaction),
            TargetEntityId = cartId,
            BeforeStateJson = JsonSerializer.Serialize(new { status = nameof(TransactionStatus.Parked), total }),
            AfterStateJson = JsonSerializer.Serialize(new { status = nameof(TransactionStatus.Voided), reason }),
        };
    }

    /// <summary>Drops lines whose item (or variant) is no longer sold and refreshes the unit price of plain catalog
    /// lines to today's price. Recalculation of totals/promos happens afterwards in the cart engine.
    /// Scope note: only plain lines are re-priced (no combo/modifier selections, not a manual override, not an
    /// open item): their price is item/variant price alone. Combo and modified lines are only checked for
    /// availability, because their stored unit price folds in selections that are not re-resolved here.</summary>
    private async Task<List<ChangedLineDto>> ReconcileLinesAsync(Transaction cart, CancellationToken cancellationToken)
    {
        var changed = new List<ChangedLineDto>();
        var lines = await transactionRepository.ListLinesAsync(cart.Id, cancellationToken);
        if (lines.Count == 0)
        {
            return changed;
        }

        var lineIds = lines.Select(line => line.Id).ToList();
        var comboByLine = (await transactionRepository.ListComboSelectionsByLinesAsync(lineIds, cancellationToken)).ToLookup(s => s.TransactionLineId);
        var modifiersByLine = (await transactionRepository.ListModifierSelectionsByLinesAsync(lineIds, cancellationToken)).ToLookup(s => s.TransactionLineId);

        var modifierIds = modifiersByLine.SelectMany(g => g).Where(s => s.ItemModifierId is not null).Select(s => s.ItemModifierId!.Value).Distinct().ToList();
        var modifiersById = (await modifierGroupRepository.ListModifiersWithGroupsByIdsAsync(modifierIds, cancellationToken))
            .ToDictionary(entry => entry.Modifier.Id, entry => entry.Modifier);
        var pickedItemIds = comboByLine.SelectMany(g => g).Select(s => s.SelectedItemId)
            .Concat(modifiersByLine.SelectMany(g => g).Where(s => s.ItemId is not null).Select(s => s.ItemId!.Value))
            .Distinct().ToList();
        var pickedItems = (await itemRepository.ListByIdsAsync(pickedItemIds, cancellationToken)).ToDictionary(item => item.Id);

        foreach (var line in lines)
        {
            if (line.IsOpenItem || line.ItemId is not { } itemId)
            {
                continue;
            }

            var item = await itemRepository.GetByIdAsync(itemId, cancellationToken);
            ItemVariant? variant = null;
            if (item is not null && item.IsActive && !item.IsDeleted && line.ItemVariantId is { } variantId)
            {
                variant = await itemVariantRepository.GetByIdAsync(variantId, cancellationToken);
            }

            var combo = comboByLine[line.Id].ToList();
            var mods = modifiersByLine[line.Id].ToList();

            var gone = item is null || !item.IsActive || item.IsDeleted || (line.ItemVariantId is not null && variant is null)
                || combo.Any(s => !pickedItems.TryGetValue(s.SelectedItemId, out var picked) || !picked.IsActive || picked.IsDeleted)
                || mods.Any(s =>
                    (s.ItemModifierId is { } mid && (!modifiersById.TryGetValue(mid, out var modifier) || !modifier.IsActive || modifier.IsDeleted))
                    || (s.ItemId is { } pid && (!pickedItems.TryGetValue(pid, out var picked2) || !picked2.IsActive || picked2.IsDeleted)));
            if (gone)
            {
                transactionRepository.RemoveLine(line);
                changed.Add(new ChangedLineDto(line.Id, Unavailable));
                continue;
            }

            if (line.PriceOverrideFrom is not null)
            {
                continue;
            }

            // Combo lines and lines with category-item picks fold selection prices into UnitPrice in ways that
            // are only resolved when the line is staged; they are not re-priced here and are flagged so the
            // cashier checks them. Plain and plain-modifier lines are re-priced from current catalog prices.
            if (item!.PricingType == PricingType.Combo || combo.Count > 0 || mods.Any(s => s.ItemId is not null))
            {
                changed.Add(new ChangedLineDto(line.Id, NotRepriced));
                continue;
            }

            var currentPrice = (variant?.PriceOverride ?? item.BasePrice)
                + mods.Where(s => s.ItemModifierId is not null).Sum(s => modifiersById[s.ItemModifierId!.Value].PriceDelta);
            if (currentPrice != line.UnitPrice)
            {
                line.UnitPrice = currentPrice;
                line.LineTotal = currentPrice * line.Quantity;
                changed.Add(new ChangedLineDto(line.Id, PriceChanged));
            }
        }

        return changed;
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Parked carts require an authenticated tenant context.");

    private Guid CurrentDeviceId => currentActorProvider.DeviceId
        ?? throw new ForbiddenException("The POS requires an authenticated device context.");

    private Guid CurrentBranchId => currentActorProvider.BranchId
        ?? throw new ForbiddenException("The POS requires an authenticated device's branch.");

    private Guid CurrentUserId => currentActorProvider.UserId
        ?? throw new InvalidOperationException("The POS requires an authenticated staff user.");
}
