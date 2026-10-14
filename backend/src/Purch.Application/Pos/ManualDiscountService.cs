using System.Text.Json;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Pos;

/// <summary>
/// Line/cart manual discounts and unit-price overrides. Only mutates the cart's stored amounts and writes the
/// audit row; the pricing itself is the cart's normal recalculation (TransactionService.RecalculateTotalAsync,
/// via ManualDiscountPricing), so a manual discount can never be priced differently from any other edit.
/// </summary>
public sealed class ManualDiscountService(
    ITransactionRepository transactionRepository,
    ITransactionService transactionService,
    IAuditLogRepository auditLogRepository,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : IManualDiscountService
{
    private const int MaxReasonLength = 200;

    public Task<TransactionDto> SetLineDiscountAsync(Guid lineId, SetManualDiscountRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var cart = await RequireDiscountableCartAsync(ct);
            var line = await RequireLineAsync(cart, lineId, ct);

            var before = line.ManualDiscountAmount;
            line.ManualDiscountAmount = ManualDiscountPricing.Amount(request.Kind, request.Value, line.LineTotal);
            Audit(AuditActionType.DiscountOverride, cart,
                new { scope = "line", lineId, manualDiscountAmount = before, total = cart.TotalAmount },
                new { scope = "line", lineId, kind = request.Kind, value = request.Value, manualDiscountAmount = line.ManualDiscountAmount, reason = request.Reason.Trim() });
            return await SaveAndPriceAsync(cart, ct);
        }, cancellationToken);
    }

    public Task<TransactionDto> SetCartDiscountAsync(SetManualDiscountRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var cart = await RequireDiscountableCartAsync(ct);
            var lines = await transactionRepository.ListLinesAsync(cart.Id, ct);

            // The cart discount comes off what is left after the line discounts (and no promotion applies beside it).
            var netAfterLines = lines.Sum(line => line.LineTotal - line.ManualDiscountAmount);
            var before = cart.ManualDiscountAmount;
            cart.ManualDiscountAmount = ManualDiscountPricing.Amount(request.Kind, request.Value, netAfterLines);
            cart.ManualDiscountReason = cart.ManualDiscountAmount > 0m ? request.Reason.Trim() : null;
            Audit(AuditActionType.DiscountOverride, cart,
                new { scope = "cart", manualDiscountAmount = before, total = cart.TotalAmount },
                new { scope = "cart", kind = request.Kind, value = request.Value, manualDiscountAmount = cart.ManualDiscountAmount, reason = request.Reason.Trim() });
            return await SaveAndPriceAsync(cart, ct);
        }, cancellationToken);
    }

    public Task<TransactionDto> OverridePriceAsync(Guid lineId, OverrideLinePriceRequest request, CancellationToken cancellationToken = default)
    {
        ValidateReason(request.Reason);
        if (request.NewUnitPrice < 0m)
        {
            throw new ValidationException(nameof(request.NewUnitPrice), "The price cannot be negative.");
        }

        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var cart = await RequireDiscountableCartAsync(ct);
            var line = await RequireLineAsync(cart, lineId, ct);

            var before = line.UnitPrice;
            var newPrice = Math.Round(request.NewUnitPrice, 2, MidpointRounding.AwayFromZero);
            // The first override remembers the catalog price; later ones keep it so the audit trail starts from it.
            line.PriceOverrideFrom ??= before;
            line.UnitPrice = newPrice;
            line.LineTotal = line.Quantity * newPrice;
            Audit(AuditActionType.PriceOverride, cart,
                new { lineId, unitPrice = before, lineTotal = before * line.Quantity },
                new { lineId, unitPrice = newPrice, lineTotal = line.LineTotal, priceOverrideFrom = line.PriceOverrideFrom, reason = request.Reason.Trim() });
            return await SaveAndPriceAsync(cart, ct);
        }, cancellationToken);
    }

    private async Task<TransactionDto> SaveAndPriceAsync(Transaction cart, CancellationToken cancellationToken)
    {
        // Flush the edit so the recalculation's line query sees it; the audit row rides in the same transaction.
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return await transactionService.RecalculateCartAsync(cart.Id, cancellationToken);
    }

    private async Task<Transaction> RequireDiscountableCartAsync(CancellationToken cancellationToken)
    {
        var deviceId = currentActorProvider.DeviceId
            ?? throw new ForbiddenException("The POS requires an authenticated device context.");

        // Only an Open cart owned by this device: a parked, kiosk-pending or finished cart is not editable here.
        var cart = await transactionRepository.GetOpenByDeviceAsync(deviceId, cancellationToken)
            ?? throw new NotFoundException("Open cart", deviceId);

        return cart.SeniorPwdDiscountApplied
            ? throw new ConflictException("A Senior Citizen/PWD discount is applied; it cannot be combined with a manual discount or price change. Remove it first.")
            : cart;
    }

    private async Task<TransactionLine> RequireLineAsync(Transaction cart, Guid lineId, CancellationToken cancellationToken)
    {
        var line = await transactionRepository.GetLineAsync(lineId, cancellationToken);
        return line is null || line.TransactionId != cart.Id
            ? throw new NotFoundException("Transaction line", lineId)
            : line;
    }

    private void Audit(AuditActionType type, Transaction cart, object before, object after)
    {
        auditLogRepository.Add(new AuditLog
        {
            TenantId = currentTenantProvider.TenantId ?? throw new InvalidOperationException("The POS requires an authenticated tenant context."),
            ActorUserId = currentActorProvider.UserId ?? throw new InvalidOperationException("The POS requires an authenticated staff user."),
            ActionType = type,
            TargetEntityType = nameof(Transaction),
            TargetEntityId = cart.Id,
            BeforeStateJson = JsonSerializer.Serialize(before),
            AfterStateJson = JsonSerializer.Serialize(after),
        });
    }

    private static void Validate(SetManualDiscountRequest request)
    {
        ValidateReason(request.Reason);
        var isPercent = string.Equals(request.Kind, ManualDiscountPricing.Percent, StringComparison.OrdinalIgnoreCase);
        if (!isPercent && !string.Equals(request.Kind, ManualDiscountPricing.Fixed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(nameof(request.Kind), "Kind must be Percent or Fixed.");
        }

        if (request.Value < 0m || (isPercent && request.Value > 100m))
        {
            throw new ValidationException(nameof(request.Value), isPercent ? "A percent discount must be between 0 and 100." : "A discount cannot be negative.");
        }
    }

    private static void ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException("reason", "A reason is required.");
        }

        if (reason.Trim().Length > MaxReasonLength)
        {
            throw new ValidationException("reason", $"The reason can be at most {MaxReasonLength} characters.");
        }
    }
}
