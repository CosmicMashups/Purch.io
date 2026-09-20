using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Promotions;

public sealed class ItemDiscountPromoRuleService(
    IItemDiscountPromoRuleRepository itemDiscountPromoRuleRepository,
    IItemRepository itemRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IItemDiscountPromoRuleService
{
    public async Task<IReadOnlyList<ItemDiscountPromoRuleDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rules = await itemDiscountPromoRuleRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        return [.. rules.OrderBy(rule => rule.Name).Select(ToDto)];
    }

    public async Task<ItemDiscountPromoRuleDto> CreateAsync(CreateItemDiscountPromoRuleRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request.Name, request.ItemId, request.DiscountType, request.DiscountValue, request.StartsAt, request.EndsAt, cancellationToken);

        var rule = new ItemDiscountPromoRule
        {
            TenantId = CurrentTenantId,
            Name = request.Name.Trim(),
            ItemId = request.ItemId,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            IsActive = true,
        };

        itemDiscountPromoRuleRepository.Add(rule);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(rule);
    }

    public async Task<ItemDiscountPromoRuleDto> UpdateAsync(Guid id, UpdateItemDiscountPromoRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = await GetOwnedRuleAsync(id, cancellationToken);

        await ValidateAsync(request.Name, request.ItemId, request.DiscountType, request.DiscountValue, request.StartsAt, request.EndsAt, cancellationToken);

        rule.Name = request.Name.Trim();
        rule.ItemId = request.ItemId;
        rule.DiscountType = request.DiscountType;
        rule.DiscountValue = request.DiscountValue;
        rule.StartsAt = request.StartsAt;
        rule.EndsAt = request.EndsAt;
        rule.IsActive = request.IsActive;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(rule);
    }

    private async Task ValidateAsync(
        string name,
        Guid itemId,
        PromoDiscountType discountType,
        decimal discountValue,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException(nameof(name), "A name is required.");
        }

        if (discountValue < 0)
        {
            throw new ValidationException(nameof(discountValue), "Discount value cannot be negative.");
        }

        if (discountType == PromoDiscountType.Percentage && discountValue > 100)
        {
            throw new ValidationException(nameof(discountValue), "A percentage discount can't exceed 100.");
        }

        if (startsAt is { } starts && endsAt is { } ends && starts >= ends)
        {
            throw new ValidationException(nameof(endsAt), "End date must be after the start date.");
        }

        _ = await GetOwnedItemAsync(itemId, cancellationToken);
    }

    private async Task<Item> GetOwnedItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var item = await itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Item", itemId);

        return item.TenantId != CurrentTenantId ? throw new NotFoundException("Item", itemId) : item;
    }

    private async Task<ItemDiscountPromoRule> GetOwnedRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        var rule = await itemDiscountPromoRuleRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("ItemDiscountPromoRule", id);

        return rule.TenantId != CurrentTenantId ? throw new NotFoundException("ItemDiscountPromoRule", id) : rule;
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Item discount promo rule management requires an authenticated tenant context.");

    private static ItemDiscountPromoRuleDto ToDto(ItemDiscountPromoRule rule)
    {
        return new(rule.Id, rule.Name, rule.ItemId, rule.DiscountType, rule.DiscountValue, rule.StartsAt, rule.EndsAt, rule.IsActive);
    }
}
