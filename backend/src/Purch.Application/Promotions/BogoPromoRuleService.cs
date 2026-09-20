using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.Application.Promotions;

public sealed class BogoPromoRuleService(
    IBogoPromoRuleRepository bogoPromoRuleRepository,
    IItemRepository itemRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IBogoPromoRuleService
{
    public async Task<IReadOnlyList<BogoPromoRuleDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rules = await bogoPromoRuleRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        return [.. rules.OrderBy(rule => rule.Name).Select(ToDto)];
    }

    public async Task<BogoPromoRuleDto> CreateAsync(CreateBogoPromoRuleRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request.Name, request.TriggerItemId, request.TriggerQuantity, request.FreeItemId, request.FreeQuantity, request.StartsAt, request.EndsAt, cancellationToken);

        var rule = new BogoPromoRule
        {
            TenantId = CurrentTenantId,
            Name = request.Name.Trim(),
            TriggerItemId = request.TriggerItemId,
            TriggerQuantity = request.TriggerQuantity,
            FreeItemId = request.FreeItemId,
            FreeQuantity = request.FreeQuantity,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            IsActive = true,
        };

        bogoPromoRuleRepository.Add(rule);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(rule);
    }

    public async Task<BogoPromoRuleDto> UpdateAsync(Guid id, UpdateBogoPromoRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = await GetOwnedRuleAsync(id, cancellationToken);

        await ValidateAsync(request.Name, request.TriggerItemId, request.TriggerQuantity, request.FreeItemId, request.FreeQuantity, request.StartsAt, request.EndsAt, cancellationToken);

        rule.Name = request.Name.Trim();
        rule.TriggerItemId = request.TriggerItemId;
        rule.TriggerQuantity = request.TriggerQuantity;
        rule.FreeItemId = request.FreeItemId;
        rule.FreeQuantity = request.FreeQuantity;
        rule.StartsAt = request.StartsAt;
        rule.EndsAt = request.EndsAt;
        rule.IsActive = request.IsActive;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(rule);
    }

    private async Task ValidateAsync(
        string name,
        Guid triggerItemId,
        int triggerQuantity,
        Guid freeItemId,
        int freeQuantity,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException(nameof(name), "A name is required.");
        }

        if (triggerQuantity <= 0)
        {
            throw new ValidationException(nameof(triggerQuantity), "Trigger quantity must be greater than zero.");
        }

        if (freeQuantity <= 0)
        {
            throw new ValidationException(nameof(freeQuantity), "Free quantity must be greater than zero.");
        }

        if (startsAt is { } starts && endsAt is { } ends && starts >= ends)
        {
            throw new ValidationException(nameof(endsAt), "End date must be after the start date.");
        }

        _ = await GetOwnedItemAsync(triggerItemId, cancellationToken);
        _ = await GetOwnedItemAsync(freeItemId, cancellationToken);
    }

    private async Task<Item> GetOwnedItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var item = await itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Item", itemId);

        return item.TenantId != CurrentTenantId ? throw new NotFoundException("Item", itemId) : item;
    }

    private async Task<BogoPromoRule> GetOwnedRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        var rule = await bogoPromoRuleRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("BogoPromoRule", id);

        return rule.TenantId != CurrentTenantId ? throw new NotFoundException("BogoPromoRule", id) : rule;
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("BOGO promo rule management requires an authenticated tenant context.");

    private static BogoPromoRuleDto ToDto(BogoPromoRule rule)
    {
        return new(rule.Id, rule.Name, rule.TriggerItemId, rule.TriggerQuantity, rule.FreeItemId, rule.FreeQuantity, rule.StartsAt, rule.EndsAt, rule.IsActive);
    }
}
