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
    IUnitOfWork unitOfWork) : ICartParkingService
{
    public const string PriceChanged = "PriceChanged";
    public const string Unavailable = "Unavailable";

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

    public Task DiscardAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExecuteInTransactionAsync<bool>(async ct =>
        {
            var cart = await RequireParkedAsync(transactionId, ct);
            auditLogRepository.Add(ExpiredAudit(cart.TenantId, CurrentUserId, cart.Id, cart.TotalAmount, "Discarded"));
            cart.Status = TransactionStatus.Voided;
            _ = await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }

    public async Task<int> ExpireStaleAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var expired = 0;
        foreach (var parked in await transactionRepository.ListParkedForExpiryAsync(cancellationToken))
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

        // Another register may have resumed, discarded or the sweep expired it between the list and this call.
        if (cart.Status != TransactionStatus.Parked)
        {
            throw new ConflictException("This cart is no longer parked.");
        }

        if (cart.BranchId != CurrentBranchId)
        {
            throw new ForbiddenException("This cart was parked at another branch.");
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
        var withSelections = (await transactionRepository.ListComboSelectionsByLinesAsync(lineIds, cancellationToken)).Select(s => s.TransactionLineId)
            .Concat((await transactionRepository.ListModifierSelectionsByLinesAsync(lineIds, cancellationToken)).Select(s => s.TransactionLineId))
            .ToHashSet();

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

            if (item is null || !item.IsActive || item.IsDeleted || (line.ItemVariantId is not null && variant is null))
            {
                transactionRepository.RemoveLine(line);
                changed.Add(new ChangedLineDto(line.Id, Unavailable));
                continue;
            }

            if (withSelections.Contains(line.Id) || item.PricingType == PricingType.Combo || line.PriceOverrideFrom is not null)
            {
                continue;
            }

            var currentPrice = variant?.PriceOverride ?? item.BasePrice;
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
